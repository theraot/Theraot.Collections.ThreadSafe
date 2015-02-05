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
        private int _count;
        private int _subindex;
        private Branch _parent;
        private object[] _entries;
        private int _offset;

        static Branch()
        {
            _branchPool = new Pool<Branch>(16, Recycle);
        }

        private Branch(int offset, Branch parent, int subindex)
        {
            _offset = offset;
            _parent = parent;
            _subindex = subindex;
            _entries = ArrayReservoir<object>.GetArray(INT_Capacity);
            _buffer = ArrayReservoir<object>.GetArray(INT_Capacity);
        }

        ~Branch()
        {
            if (!AppDomain.CurrentDomain.IsFinalizingForUnload())
            {
                ArrayReservoir<object>.DonateArray(_entries);
                ArrayReservoir<object>.DonateArray(_buffer);
            }
        }

        public static Branch Create(int offset, Branch parent, int subindex)
        {
            Branch result;
            if (_branchPool.TryGet(out result))
            {
                result._offset = offset;
                result._parent = parent;
                result._subindex = subindex;
                result._entries = ArrayReservoir<object>.GetArray(INT_Capacity);
                result._buffer = ArrayReservoir<object>.GetArray(INT_Capacity);
                return result;
            }
            return new Branch(offset, parent, subindex);
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

        public bool RemoveAt(uint index, out object previous)
        {
            previous = null;
            // Get the target branch  - can be null
            var branch = Map(index, true);
            // Check if we got a branch
            if (branch == null)
            {
                // We didn't get a branch, meaning that what we look for is not there
                return false;
            }
            // ---
            if (branch.PrivateRemoveAt(index, out previous))
            {
                branch.Shrink();
                return true;
            }
            return false;
        }

        private void Shrink()
        {
            if
                (
                    _parent != null
                    && Interlocked.CompareExchange(ref _parent._buffer[_subindex], this, null) == null
                    && Interlocked.CompareExchange(ref _count, 0, 0) == 0
                    && Interlocked.CompareExchange(ref _parent._entries[_subindex], null, this) == this
                )
            {
                if (Interlocked.CompareExchange(ref _count, 0, 0) == 0)
                {
                    var found = Interlocked.CompareExchange(ref _parent._buffer[_subindex], null, this);
                    if (found == this)
                    {
                        var parent = _parent;
                        _branchPool.Donate(this);
                        parent.Shrink();
                    }
                }
                else
                {
                    // TODO: test
                    var found = Interlocked.CompareExchange(ref _parent._entries[_subindex], _parent._buffer[_subindex], null);
                    if (found != null)
                    {
                        _branchPool.Donate(this);
                    }
                }
            }
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

        private static void Recycle(Branch branch)
        {
            branch._entries = null;
            branch._buffer = null;
            branch._count = 0;
            branch._parent = null;
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
            if (node is Branch)
            {
                return node as Branch;
            }
            var branch = Create(offset, this, subindex);
            var result = Interlocked.CompareExchange(ref _buffer[subindex], branch, null) as Branch;
            if (result == null)
            {
                result = branch;
            }
            else
            {
                _branchPool.Donate(branch);
            }
            var found = Interlocked.CompareExchange(ref _entries[subindex], result, node);
            if (found == node)
            {
                Interlocked.Exchange(ref _buffer[subindex], null);
                return result;
            }
            return this;
            // return (Branch)found;
        }

        private Branch Map(uint index, bool readOnly)
        {
            // do we need a leaf?
            if (_offset == 0)
            {
                // It is not responsability of this method to handle leafs
                return this;
            }
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
            // We can write
            return Grow(index).Map(index, false);
        }

        private bool PrivateExchange(uint index, object item, out object previous)
        {
            previous = null;
            var subindex = GetSubindex(index);
            object _previous = Interlocked.Exchange(ref _entries[subindex], Leaf.Create(index, item));
            if (_previous == null)
            {
                Interlocked.Increment(ref _count);
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
                Interlocked.Increment(ref _count);
                return true;
            }
            previous = ((Leaf)_previous).Value;
            return false;
        }

        private bool PrivateRemoveAt(uint index, out object previous)
        {
            var subindex = GetSubindex(index);
            previous = Interlocked.Exchange(ref _entries[subindex], null);
            if (previous == null)
            {
                return false;
            }
            previous = ((Leaf)previous).Value;
            Interlocked.Decrement(ref _count);
            return true;
        }

        private void PrivateSet(uint index, object item, out bool isNew)
        {
            var subindex = GetSubindex(index);
            isNew = false;
            object _previous = Interlocked.Exchange(ref _entries[subindex], Leaf.Create(index, item));
            if (_previous == null)
            {
                Interlocked.Increment(ref _count);
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