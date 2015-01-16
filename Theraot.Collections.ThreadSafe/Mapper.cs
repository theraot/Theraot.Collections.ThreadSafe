using System;
using System.Threading;

namespace Theraot.Collections.ThreadSafe
{
    public class Mapper<T>
    {
        private const int INT_Capacity = 16;
        private const int INT_OffsetStep = 4;
        private const int INT_MaxOffset = 32;
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

            private Branch(uint mask, uint index)
                : base(index & mask)
            {
                _mask = mask;
            }

            public Branch(int offset, uint index)
                : this(unchecked((uint)(1 << offset) - 1), index)
            {
                _offset = offset;
                children = new Bucket<Node>(INT_Capacity);
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
                    var subindex = (int)((index >> _offset) & 0xF);
                    Node node;
                    if (children.TryGet(subindex, out node))
                    {
                        return node.TrySet(index, value);
                    }
                    if (_offset == INT_MaxOffset)
                    {
                        // TODO: what if Insert fails, Insert may fail if the index is already used
                        return children.Insert(subindex, new Leaf(value, index));
                    }
                    else
                    {
                        var branch = new Branch(_offset + INT_OffsetStep, index);
                        // TODO: what if Insert fails, Insert may fail if the index is already used
                        children.Insert(subindex, branch);
                        return branch.TrySet(index, value);
                    }
                }
                return false;
            }
        }

        private class Leaf : Node
        {
            private T _value;
            private readonly object _synclock;

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