using System;
using System.Collections.Generic;
using System.Threading;

namespace Theraot.Collections.ThreadSafe
{
    internal class Branch : IEnumerable<object>
    {
        private const int INT_Capacity = 1 << INT_OffsetStep;
        private const int INT_OffsetStep = 4;

        private static readonly Pool<Branch> _branchPool;
        private object[] _buffer;
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
                        branch._buffer = ArrayReservoir<object>.GetArray(INT_Capacity);
                    }
                );
        }

        private Branch(int offset)
        {
            _offset = offset;
            _entries = ArrayReservoir<object>.GetArray(INT_Capacity);
            _buffer = ArrayReservoir<object>.GetArray(INT_Capacity);
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
            // ---
            return branch.PrivateExchange(index, item, out previous);
        }

        public IEnumerator<object> GetEnumerator()
        {
            foreach (var child in _entries)
            {
                if (!ReferenceEquals(child, null))
                {
                    var items = child as Branch;
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            yield return item;
                        }
                    }
                    else
                    {
                        yield return child;
                    }
                }
            }
        }

        public bool Insert(uint index, object item, out object previous)
        {
            // Get the target branch - can only be null if we request readonly - we did not
            Branch branch = Map(index, false);
            // ---
            return branch.PrivateInsert(index, item, out previous);
            // if this returns true, the new item was inserted, so there was no previous item
            // if this returns false, something was inserted first... so we get the previous item
        }

        public void Set(uint index, object value, out bool isNew)
        {
            // Get the target branch - can only be null if we request readonly - we did not
            Branch branch = Map(index, false);
            // ---
            branch.PrivateSet(index, value, out isNew);
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
            // ---
            return branch.PrivateTryGet(index, out value);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private int GetSubindex(uint index)
        {
            return (int)((index >> _offset) & 0xF);
        }

        private Branch Grow(uint index)
        {
            var offset = _offset - INT_OffsetStep;
            var subindex = GetSubindex(index);
            var node = _entries[subindex];
            var leaf = node as Leaf;
            if (node is Branch)
            {
                return node as Branch;
            }
            if (leaf != null)
            {
                var branch = Create(offset);
                var result = Interlocked.CompareExchange(ref _buffer[subindex], branch, null) as Branch;
                if (result == null)
                {
                    bool isNew;
                    branch.PrivateSet(leaf.Index, branch, out isNew);
                    Interlocked.CompareExchange(ref _entries[subindex], branch, node);
                    Interlocked.Exchange(ref _buffer[subindex], null);
                    return branch;
                }
                _branchPool.Donate(branch);
                return result;
            }
            {
                var branch = Create(offset);
                var result = Interlocked.CompareExchange(ref _buffer[subindex], branch, null) as Branch;
                if (result == null)
                {
                    var found = Interlocked.CompareExchange(ref _entries[subindex], branch, node);
                    if (found == node)
                    {
                        Interlocked.Exchange(ref _buffer[subindex], null);
                        return branch;
                    }
                    return (Branch)found;
                }
                _branchPool.Donate(branch);
                return result;
            }
        }

        private Branch Map(uint index, bool readOnly)
        {
            object result;
            if (PrivateTryGetBranch(index, out result))
            {
                var leaf = result as Leaf;
                if (leaf == null)
                {
                    var branch = result as Branch;
                    if (branch == null)
                    {
                        return this;
                    }
                    return branch.Map(index, readOnly);
                }
                if (leaf.Index == index)
                {
                    return this;
                }
            }
            if (readOnly)
            {
                return null;
            }
            // We can write, do we need a leaf?
            if (_offset != 0)
            {
                return Grow(index).Map(index, false);
            }
            // We need to insert a leaf
            // It is not responsability of this method to create leafs
            return this;
        }

        private bool PrivateExchange(uint index, object item, out object previous)
        {
            previous = null;
            var subindex = GetSubindex(index);
            object _previous = Interlocked.Exchange(ref _entries[subindex], Leaf.Create(index, item));
            if (_previous == null)
            {
                return true;
            }
            var leaf = ((Leaf)_previous);
            previous = leaf.Value;
            Leaf.Donate(leaf);
            return false;
        }

        private bool PrivateInsert(uint index, object item, out object previous)
        {
            var subindex = GetSubindex(index);
            previous = null;
            object _previous = Interlocked.CompareExchange(ref _entries[subindex], Leaf.Create(index, item), null);
            if (_previous == null)
            {
                return true;
            }
            previous = ((Leaf)_previous).Value;
            return false;
        }

        private void PrivateSet(uint index, object item, out bool isNew)
        {
            var subindex = GetSubindex(index);
            isNew = false;
            object _previous = Interlocked.Exchange(ref _entries[subindex], Leaf.Create(index, item));
            if (_previous == null)
            {
                isNew = true;
            }
            else
            {
                var leaf = ((Leaf)_previous);
                Leaf.Donate(leaf);
            }
        }

        private bool PrivateTryGet(uint index, out object previous)
        {
            var subindex = GetSubindex(index);
            previous = null;
            var _previous = Interlocked.CompareExchange(ref _entries[subindex], null, null);
            if (_previous == null)
            {
                return false;
            }
            previous = ((Leaf)_previous).Value;
            return true;
        }

        private bool PrivateTryGetBranch(uint index, out object previous)
        {
            var subindex = GetSubindex(index);
            previous = Interlocked.CompareExchange(ref _entries[subindex], null, null);
            if (previous == null)
            {
                return false;
            }
            return true;
        }
    }
}