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
            private readonly Bucket<Node> _children;
            private readonly uint _mask;
            private readonly int _offset;

            public Branch(int offset, uint index)
                : this(unchecked((uint)(1 << offset) - 1), index)
            {
                _offset = offset;
                _children = new Bucket<Node>(INT_Capacity);
            }

            private Branch(uint mask, uint index)
                : base(index & mask)
            {
                _mask = mask;
            }

            public override bool TryGet(uint index, out T value)
            {
                value = default(T);
                if ((index & _mask) != Index)
                {
                    // This fails because the index was wrong
                    // On failure, isNew must be false
                    return false;
                }
                // Get the target branch to which to insert
                Branch branch = Map(index, true);
                // Check if we got a branch
                if (branch == null)
                {
                    // We didn't get a branch, meaning that what we look for is not there
                    return false;
                }
                else
                {
                    // We got a branch, attempt to read it
                    Node node;
                    var children = branch._children;
                    var subindex = (int)((index >> branch._offset) & 0xF);
                    if (children.TryGet(subindex, out node))
                    {
                        // We found the leaf, read it to get the value
                        return node.TryGet(index, out value);
                    }
                    // We didn't get the leaf, it may have been removed
                    return false;
                }
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
                // Get the target branch to which to insert
                Branch branch = Map(index, false);
                // The branch will only be null if we request readonly
                var children = branch._children;
                var subindex = (int)((index >> branch._offset) & 0xF);
                // Insert leaf
                children.Set(subindex, new Leaf(value, index), out isNew);
                // if this returns true, the new item was inserted, so isNew is set to true
                // if this returns false, some other thread inserted first... so isNew is set to false
                // yet we pretend we inserted first and the value was replaced by the other thread
                // So we say we did it regardless
                return true;
            }

            private Branch Map(uint index, bool readOnly)
            {
                Node result;
                // Calculate the index of the target child
                var subindex = (int)((index >> _offset) & 0xF);
                // Retrieve the already present branch
                if (_children.TryGet(subindex, out result))
                {
                    // We success in retrieving the branch
                    var branch = result as Branch;
                    if (branch == null)
                    {
                        // Return this
                        return this;
                    }
                    else
                    {
                        // Delegate to it
                        return branch.Map(index, readOnly);
                    }
                }
                else
                {
                    // We fail to retrieve the branch because it is not there
                    if (readOnly)
                    {
                        // We cannot write, so we don't attempt to isnert a new node
                        // return null instead
                        return null;
                    }
                    else
                    {
                        // Try to insert a new one
                        if (_offset == INT_MaxOffset)
                        {
                            // We need to insert a leaf
                            // It is not responsability of this method to create leafs
                            return this;
                        }
                        else
                        {
                            // We need to insert a branch
                            // Create the branch to insert
                            var branch = new Branch(_offset + INT_OffsetStep, index);
                        again:
                            // Attempt to insert the created branch
                            if (_children.Insert(subindex, branch))
                            {
                                // We success in inserting the branch
                                // Delegate to the new branch
                                return branch.Map(index, false);
                            }
                            else
                            {
                                // We did fail in inserting the branch because another thread inserted one
                                // Note: We do not jump out to start over...
                                //       because we have already created a branch, and we may need it
                                // Retrieve the already present branch
                                if (_children.TryGet(subindex, out result))
                                {
                                    // We success in retrieving the branch
                                    // We are leaking the Branch
                                    // TODO: solve leak
                                    branch = result as Branch;
                                    if (branch == null)
                                    {
                                        // Return this
                                        return this;
                                    }
                                    else
                                    {
                                        // Delegate to it
                                        return branch.Map(index, true);
                                    }
                                }
                                else
                                {
                                    // We fail to retrieve the branch because another thread must have removed it
                                    // Start over, we have a chance to insert the branch back again
                                    // Jump back to where we just created the branch to attempt to insert it again
                                    goto again;
                                    // This creates a loop
                                    // TODO: solve loop
                                }
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