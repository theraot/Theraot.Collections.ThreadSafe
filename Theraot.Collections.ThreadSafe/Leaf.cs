namespace Theraot.Collections.ThreadSafe
{
    internal class Leaf
    {
        private static readonly Pool<Leaf> _leafPool;
        private uint _index;
        private object _value;

        static Leaf()
        {
            _leafPool = new Pool<Leaf>
                (
                    16,
                    leaf =>
                    {
                        leaf._value = false;
                    }
                );
        }

        private Leaf(uint index, object value)
        {
            _index = index;
            _value = value;
        }

        public uint Index
        {
            get
            {
                return _index;
            }
        }

        public object Value
        {
            get
            {
                return _value;
            }
        }

        public static Leaf Create(uint index, object value)
        {
            Leaf result;
            if (_leafPool.TryGet(out result))
            {
                result._index = index;
                result._value = value;
                return result;
            }
            return new Leaf(index, value);
        }

        public static void Donate(Leaf leaf)
        {
            _leafPool.Donate(leaf);
        }
    }
}