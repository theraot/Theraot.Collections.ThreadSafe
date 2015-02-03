using System;
using System.Collections.Generic;
using System.Threading;

namespace Theraot.Collections.ThreadSafe
{
    internal class Branch : INode, IEnumerable<object>
    {
        private const int INT_Capacity = 1 << INT_OffsetStep;
        private const int INT_OffsetStep = 4;

        private static readonly Pool<Branch> _branchPool;
        private int _count;
        private object[] _entries;
        private int _offset;

        static Branch()
        {
            _branchPool = new Pool<Branch>
                (
                    16,
                    branch =>
                    {
                        branch._entries = ArrayReservoir<object>.GetArray(INT_Capacity);
                    }
                );
        }

        private Branch(int offset)
        {
            _offset = offset;
            _entries = ArrayReservoir<object>.GetArray(INT_Capacity);
        }

        ~Branch()
        {
            if (!AppDomain.CurrentDomain.IsFinalizingForUnload())
            {
                ArrayReservoir<object>.DonateArray(_entries);
            }
        }

        public static Branch Create(int offset)
        {
            Branch result;
            if (_branchPool.TryGet(out result))
            {
                result._offset = offset;
                return result;
            }
            return new Branch(offset);
        }

        public bool Exchange(uint index, object item, out object previous)
        {
            // Get the target branch - can only be null if we request readonly - we did not
            Branch branch = Map(index, false);
            // Get the subindex
            var subindex = GetSubindex(index, branch);
            // ---
            return branch.PrivateExchange(subindex, item, out previous);
        }

        public IEnumerator<object> GetEnumerator()
        {
            if (_offset == 0)
            {
                foreach (var child in _entries)
                {
                    if (!ReferenceEquals(child, null) && !ReferenceEquals(child, BucketHelper.Null))
                    {
                        yield return child;
                    }
                }
            }
            else
            {
                foreach (var child in _entries)
                {
                    var items = child as Branch;
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            yield return item;
                        }
                    }
                }
            }
        }

        public bool Insert(uint index, object item, out object previous)
        {
            // Get the target branch - can only be null if we request readonly - we did not
            Branch branch = Map(index, false);
            // Get the subindex
            var subindex = GetSubindex(index, branch);
            // ---
            return branch.PrivateInsert(subindex, item, out previous);
            // if this returns true, the new item was inserted, so there was no previous item
            // if this returns false, something was inserted first... so we get the previous item
        }

        public void Set(uint index, object value, out bool isNew)
        {
            // Get the target branch - can only be null if we request readonly - we did not
            Branch branch = Map(index, false);
            // Get the subindex
            var subindex = GetSubindex(index, branch);
            // ---
            branch.PrivateSet(subindex, value, out isNew);
            // if this returns true, the new item was inserted, so isNew is set to true
            // if this returns false, some other thread inserted first... so isNew is set to false
            // yet we pretend we inserted first and the value was replaced by the other thread
            // So we say the operation was a success regardless
        }

        public bool TryGet(uint index, out object value)
        {
            value = null;
            // Get the target branch  - can be null
            var branch = Map(index, true);
            // Check if we got a branch
            if (branch == null)
            {
                // We didn't get a branch, meaning that what we look for is not there
                return false;
            }
            // Get the subindex
            var subindex = GetSubindex(index, branch);
            // ---
            return branch.PrivateTryGet(subindex, out value);
        }

        private static int GetSubindex(uint index, Branch branch)
        {
            return (int)((index >> branch._offset) & 0xF);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private Branch Map(uint index, bool readOnly)
        {
            var subindex = GetSubindex(index, this);
            object result;
            if (PrivateTryGet(subindex, out result))
            {
                var branch = result as Branch;
                if (branch == null)
                {
                    return this;
                }
                return branch.Map(index, readOnly);
            }
            if (readOnly)
            {
                return null;
            }
            // We can write, do we need a leaf?
            if (_offset != 0)
            {
                // We need to insert a branch
                // Create the branch to insert
                var branch = Create(_offset - INT_OffsetStep);
                // TODO: solve loop
                while (true)
                {
                    // Attempt to insert the created branch
                    if (PrivateInsert(subindex, branch))
                    {
                        // We success in inserting the branch
                        // Delegate to the new branch
                        return branch.Map(index, false);
                    }
                    // We did fail in inserting the branch because another thread inserted one
                    // Note: We do not jump out to start over...
                    //       because we have already created a branch, and we may need it
                    // Retrieve the already present branch
                    if (PrivateTryGet(subindex, out result))
                    {
                        // We success in retrieving the branch
                        // We are leaking the Branch
                        _branchPool.Donate(branch);
                        branch = result as Branch;
                        if (branch == null)
                        {
                            // Return this
                            return this;
                        }
                        // Delegate to it
                        return branch.Map(index, true);
                    }
                    // We fail to retrieve the branch because another thread must have removed it
                    // Start over, we have a chance to insert the branch back again
                    // Jump back to where we just created the branch to attempt to insert it again
                }
            }
            // We need to insert a leaf
            // It is not responsability of this method to create leafs
            return this;
        }

        private void PrivateSet(int index, object item, out bool isNew)
        {
            isNew = Interlocked.Exchange(ref _entries[index], item ?? BucketHelper.Null) == null;
            if (isNew)
            {
                Interlocked.Increment(ref _count);
            }
        }

        private bool PrivateExchange(int index, object item, out object previous)
        {
            previous = null;
            object _previous = Interlocked.Exchange(ref _entries[index], item ?? BucketHelper.Null);
            if (_previous == null)
            {
                Interlocked.Increment(ref _count);
                return true;
            }
            if (!ReferenceEquals(_previous, BucketHelper.Null))
            {
                previous = _previous;
            }
            return false;
        }

        private bool PrivateInsert(int index, object item)
        {
            object _previous = Interlocked.CompareExchange(ref _entries[index], item ?? BucketHelper.Null, null);
            if (_previous == null)
            {
                Interlocked.Increment(ref _count);
                return true;
            }
            return false;
        }

        private bool PrivateInsert(int index, object item, out object previous)
        {
            previous = null;
            object _previous = Interlocked.CompareExchange(ref _entries[index], item ?? BucketHelper.Null, null);
            if (_previous == null)
            {
                Interlocked.Increment(ref _count);
                return true;
            }
            if (!ReferenceEquals(_previous, BucketHelper.Null))
            {
                previous = _previous;
            }
            return false;
        }

        private bool PrivateTryGet(int index, out object value)
        {
            value = null;
            var entry = Interlocked.CompareExchange(ref _entries[index], null, null);
            if (entry == null)
            {
                return false;
            }
            if (!ReferenceEquals(entry, BucketHelper.Null))
            {
                value = entry;
            }
            return true;
        }
    }
}