using System.Threading;

namespace Theraot.Collections.ThreadSafe
{
    public class Mapper<T>
    {
        private const int INT_Capacity = 16;
        private const int INT_MaxOffset = 32;
        private const int INT_OffsetStep = 4;
        private readonly Branch root;

        public Mapper()
        {
            root = new Branch(0, 0);
        }

        public bool TryGet(int index, out T value)
        {
            return root.TryGet(unchecked((uint)index), out value);
        }

        public bool TrySet(int index, T value)
        {
            return root.TrySet(unchecked((uint)index), value);
        }

        private class Branch : Node
        {
            private readonly uint _mask;
            private readonly int _offset;
            private readonly Bucket<Node> children;

            public Branch(int offset, uint index)
                : this(unchecked((uint)(1 << offset) - 1), index)
            {
                _offset = offset;
                children = new Bucket<Node>(INT_Capacity);
            }

            private Branch(uint mask, uint index)
                : base(index & mask)
            {
                _mask = mask;
            }

            public override bool TryGet(uint index, out T value)
            {
                value = default(T);
                if ((index & _mask) == Index)
                {
                    var subindex = (int)((index >> _offset) & 0xF);
                    Node node;
                    if (children.TryGet(subindex, out node))
                    {
                        return node.TryGet(index, out value);
                    }
                }
                return false;
            }

            public override bool TrySet(uint index, T value)
            {
                if ((index & _mask) == Index)
                {
                    Node node;
                    // Calculate the index of the target child
                    var subindex = (int)((index >> _offset) & 0xF);
                    // Retrieve the already present branch
                    if (children.TryGet(subindex, out node))
                    {
                        // We success in retrieving the branch
                        // Write to it
                        if (node.TrySet(index, value))
                        {
                            // We success
                            return true;
                        }
                        else
                        {
                            // We were unable to write
                            return false;
                        }
                    }
                    else
                    {
                        // We fail to retrieve the branch because it is not there
                        // Try to insert a new one
                        if (_offset == INT_MaxOffset)
                        {
                            // We nned to insert a leaf
                            children.Insert(subindex, new Leaf(value, index));
                            // if this returns true, the new item was inserted
                            // if this returns false, some other thread inserted first...
                            // yet we pretend we inserted first and the value was replaced by the other thread
                            // So we say we did it
                            return true;
                        }
                        else
                        {
                            // We need to insert a branch
                            // Create the branch to insert
                            var branch = new Branch(_offset + INT_OffsetStep, index);
                        again:
                            // Attempt to insert the created branch
                            if (children.Insert(subindex, branch))
                            {
                                // We success in inserting the branch
                                // Now write to the inserted branch
                                if (branch.TrySet(index, value))
                                {
                                    // We success
                                    return true;
                                }
                                else
                                {
                                    // We were unable to write
                                    return false;
                                }
                            }
                            else
                            {
                                // We did fail in inserting the branch because another thread inserted one
                                // Note: We do not jump out to start over...
                                //       because we have already created a branch, and we may need it
                                // Retrieve the already present branch
                                if (children.TryGet(subindex, out node))
                                {
                                    // We success in retrieving the branch
                                    // Write to it
                                    node.TrySet(index, value);
                                }
                                else
                                {
                                    // We fail to retrieve the branch because another thread must have removed it
                                    // Start over, we have a chance to insert the branch back again
                                    // Jump back to where we just created the branch to attemp to insert it again
                                    goto again;
                                    // This creates a loop
                                    // TODO: solve loop
                                }
                            }
                        }
                    }
                    return true;
                }
                return false;
            }
        }

        private class Leaf : Node
        {
            private readonly object _synclock;
            private T _value;

            public Leaf(T value, uint index)
                : base(index)
            {
                _value = value;
                _synclock = new object();
            }

            public override bool TryGet(uint index, out T value)
            {
                if (index == Index)
                {
                    value = _value;
                    return true;
                }
                value = default(T);
                // This fails because the index was wrong
                return false;
            }

            public override bool TrySet(uint index, T value)
            {
                if (index == Index)
                {
                    bool got = false;
                    try
                    {
                        if (Monitor.TryEnter(_synclock))
                        {
                            got = true;
                            _value = value;
                            return true;
                        }
                    }
                    finally
                    {
                        if (got)
                        {
                            Monitor.Exit(_synclock);
                        }
                    }
                }
                // This fails because the index was wrong or because another thread took the adventage
                return false;
            }
        }

        private abstract class Node
        {
            private readonly uint _index;

            protected Node(uint index)
            {
                _index = index;
            }

            protected uint Index
            {
                get
                {
                    return _index;
                }
            }

            public abstract bool TryGet(uint index, out T value);

            public abstract bool TrySet(uint index, T value);
        }
    }
}