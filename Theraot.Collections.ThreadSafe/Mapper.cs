namespace Theraot.Collections.ThreadSafe
{
    public class Mapper<T>
    {
        private const int INT_Capacity = 16;
        private readonly Branch root;

        public Mapper()
        {
            root = new Branch(0x00000000, 0);
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
                : base(index)
            {
                _offset = offset;
                _mask = unchecked((uint)(1 << _offset) - 1);
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
                    if (_offset == 32)
                    {
                        children.Insert(subindex, new Leaf(value, index));
                        return true;
                    }
                    else
                    {
                        var refinedMask = unchecked((uint)(1 << (_offset + 4)) - 1);
                        var refinedIndex = index & refinedMask;
                        var banch = new Branch(_offset + 4, refinedIndex);
                        children.Insert(subindex, banch);
                        return banch.TrySet(index, value);
                    }
                }
                return false;
            }
        }

        private class Leaf : Node
        {
            private T _value;

            public Leaf(T value, uint index)
                : base(index)
            {
                _value = value;
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
                    _value = value;
                    return true;
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