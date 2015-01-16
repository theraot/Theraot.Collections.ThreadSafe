using System.Threading;

namespace Theraot.Collections.ThreadSafe
{
    public class Mapper<T>
    {
        private const int INT_Capacity = 16;
        private const int INT_MaxOffset = 32;
        private const int INT_OffsetStep = 4;
        private readonly Branch _root;
        private int _count;

        public Mapper()
        {
            _count = 0;
            _root = new Branch(0, 0);
        }

        /// <summary>
        /// Gets the number of items actually contained.
        /// </summary>
        public int Count
        {
            get
            {
                return _count;
            }
        }

        public bool TryGet(int index, out T value)
        {
            return _root.TryGet(unchecked((uint)index), out value);
        }

        public bool TrySet(int index, T value, out bool isNew)
        {
            var result = _root.TrySet(unchecked((uint)index), value, out isNew);
            if (isNew)
            {
                Interlocked.Increment(ref _count);
            }
            return result;
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

            public override bool TrySet(uint index, T value, out bool isNew)
            {
                if ((index & _mask) != Index)
                {
                    // This fails because the index was wrong
                    // On failure, isNew must be false
                    isNew = false;
                    return false;
                }
                Node node;
                // Calculate the index of the target child
                var subindex = (int)((index >> _offset) & 0xF);
                // Retrieve the already present branch
                if (children.TryGet(subindex, out node))
                {
                    // We success in retrieving the branch
                    // Write to it
                    if (node.TrySet(index, value, out isNew))
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
                        isNew = true;
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
                            if (branch.TrySet(index, value, out isNew))
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
                                node.TrySet(index, value, out isNew);
                                // We are leaking the Branch
                                // TODO: solve leak
                                return true;
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
                if (index != Index)
                {
                    // This fails because the index was wrong
                    value = default(T);
                    return false;
                }
                value = _value;
                return true;
            }

            public override bool TrySet(uint index, T value, out bool isNew)
            {
                // We assume that the leaf only exists because it has a vale...
                // So, no. This is not new.
                isNew = false;
                if (index != Index)
                {
                    // This fails because the index was wrong
                    return false;
                }
                var got = false;
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
                // This fails because another thread took the adventage
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

            public abstract bool TrySet(uint index, T value, out bool isNew);
        }
    }
}