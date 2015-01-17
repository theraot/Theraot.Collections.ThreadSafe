using System;
using System.Collections.Generic;
using System.Threading;

namespace Theraot.Collections.ThreadSafe
{
    public class Mapper<T> : IEnumerable<T>
    {
        private const int INT_Capacity = 16;
        private const int INT_MaxOffset = 32;
        private const int INT_OffsetStep = 4;
        private readonly Branch _root;
        private int _count;

        public Mapper()
        {
            _count = 0;
            _root = new Branch(0);
        }

        private interface INode
        {
            // Empty
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

        /// <summary>
        /// Copies the items to a compatible one-dimensional array, starting at the specified index of the target array.
        /// </summary>
        /// <param name="array">The array.</param>
        /// <param name="arrayIndex">Index of the array.</param>
        /// <exception cref="System.ArgumentNullException">array</exception>
        /// <exception cref="System.ArgumentOutOfRangeException">arrayIndex;Non-negative number is required.</exception>
        /// <exception cref="System.ArgumentException">array;The array can not contain the number of elements.</exception>
        public void CopyTo(T[] array, int arrayIndex)
        {
            if (array == null)
            {
                throw new ArgumentNullException("array");
            }
            if (arrayIndex < 0)
            {
                throw new ArgumentOutOfRangeException("arrayIndex", "Non-negative number is required.");
            }
            if (_count > array.Length - arrayIndex)
            {
                throw new ArgumentException("The array can not contain the number of elements.", "array");
            }
            try
            {
                foreach (var entry in _root)
                {
                    array[arrayIndex] = entry;
                    arrayIndex++;
                }
            }
            catch (IndexOutOfRangeException exception)
            {
                throw new ArgumentOutOfRangeException("array", exception.Message);
            }
        }

        /// <summary>
        /// Sets the item at the specified index.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <param name="item">The item.</param>
        /// <param name="previous">The previous item in the specified index.</param>
        /// <returns>
        ///   <c>true</c> if the item was new; otherwise, <c>false</c>.
        /// </returns>
        /// <exception cref="System.ArgumentOutOfRangeException">index;index must be greater or equal to 0 and less than capacity</exception>
        public bool Exchange(int index, T item, out T previous)
        {
            if (_root.Exchange(unchecked((uint)index), item, out previous))
            {
                Interlocked.Increment(ref _count);
                return true;
            }
            return false;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _root.GetEnumerator();
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool TryGet(int index, out T value)
        {
            return _root.TryGet(unchecked((uint)index), out value);
        }

        public void TrySet(int index, T value, out bool isNew)
        {
            _root.TrySet(unchecked((uint)index), value, out isNew);
            if (isNew)
            {
                Interlocked.Increment(ref _count);
            }
        }

        private struct Leaf : INode
        {
            private readonly T _value;

            public Leaf(T value)
            {
                _value = value;
            }

            public T Value
            {
                get
                {
                    return _value;
                }
            }
        }

        private class Branch : INode, IEnumerable<T>
        {
            private readonly Bucket<INode> _children;

            private readonly int _offset;

            public Branch(int offset)
            {
                _offset = offset;
                _children = new Bucket<INode>(INT_Capacity);
            }

            public bool Exchange(uint index, T item, out T previous)
            {
                // Get the target branch with which to exchange
                Branch branch = Map(index, false);
                // The branch will only be null if we request readonly - we did not
                var children = branch._children;
                var subindex = (int)((index >> branch._offset) & 0xF);
                INode _previous;
                var isNew = children.Exchange(subindex, new Leaf(item), out _previous);
                previous = isNew ? default(T) : ((Leaf)_previous).Value;
                return isNew;
            }

            public IEnumerator<T> GetEnumerator()
            {
                if (_offset == INT_MaxOffset)
                {
                    foreach (var child in _children)
                    {
                        yield return ((Leaf)child).Value;
                    }
                }
                else
                {
                    foreach (var child in _children)
                    {
                        foreach (var item in ((Branch)child))
                        {
                            yield return item;
                        }
                    }
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public bool TryGet(uint index, out T value)
            {
                value = default(T);
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
                    INode node;
                    var children = branch._children;
                    var subindex = (int)((index >> branch._offset) & 0xF);
                    if (children.TryGet(subindex, out node))
                    {
                        // We found the leaf, read it to get the value
                        value = ((Leaf)node).Value;
                        return true;
                    }
                    // We didn't get the leaf, it may have been removed
                    return false;
                }
            }

            public void TrySet(uint index, T value, out bool isNew)
            {
                // Get the target branch to which to insert
                Branch branch = Map(index, false);
                // The branch will only be null if we request readonly - we did not
                var children = branch._children;
                var subindex = (int)((index >> branch._offset) & 0xF);
                // Insert leaf
                children.Set(subindex, new Leaf(value), out isNew);
                // if this returns true, the new item was inserted, so isNew is set to true
                // if this returns false, some other thread inserted first... so isNew is set to false
                // yet we pretend we inserted first and the value was replaced by the other thread
                // So we say the operation was a success regardless
            }

            private Branch Map(uint index, bool readOnly)
            {
                INode result;
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
                            var branch = new Branch(_offset + INT_OffsetStep);
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
    }
}