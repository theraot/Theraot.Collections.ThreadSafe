using System.Collections.Generic;

namespace Theraot.Collections.ThreadSafe
{
    public partial class Mapper<T>
    {
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
                var subindex = GetSubindex(index, branch);
                INode _previous;
                var isNew = children.Exchange(subindex, new Leaf(item), out _previous);
                previous = isNew ? default(T) : ((Leaf)_previous).Value;
                return isNew;
            }

            public IEnumerator<T> GetEnumerator()
            {
                if (_offset == 0)
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
                        foreach (var item in (Branch)child)
                        {
                            yield return item;
                        }
                    }
                }
            }

            public bool Insert(uint index, T item, out T previous)
            {
                // Get the target branch to which to insert
                Branch branch = Map(index, false);
                // The branch will only be null if we request readonly - we did not
                var children = branch._children;
                var subindex = GetSubindex(index, branch);
                // Insert leaf
                INode previousLeaf;
                var result = children.InsertExtracted(subindex, new Leaf(item), out previousLeaf);
                previous = result ? default(T) : ((Leaf)previousLeaf).Value;
                return result;
                // if this returns true, the new item was inserted, so there was no previous item
                // if this returns false, something was inserted first... so we get the previous item
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
                    var subindex = GetSubindex(index, branch);
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
                var subindex = GetSubindex(index, branch);
                // Insert leaf
                children.Set(subindex, new Leaf(value), out isNew);
                // if this returns true, the new item was inserted, so isNew is set to true
                // if this returns false, some other thread inserted first... so isNew is set to false
                // yet we pretend we inserted first and the value was replaced by the other thread
                // So we say the operation was a success regardless
            }

            private static int GetSubindex(uint index, Branch branch)
            {
                return (int)((index >> branch._offset) & 0xF);
            }

            private Branch Map(uint index, bool readOnly)
            {
                INode result;
                // Calculate the index of the target child
                var subindex = GetSubindex(index, this);
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
                        if (_offset == 0)
                        {
                            // We need to insert a leaf
                            // It is not responsability of this method to create leafs
                            return this;
                        }
                        else
                        {
                            // We need to insert a branch
                            // Create the branch to insert
                            var branch = new Branch(_offset - INT_OffsetStep);
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