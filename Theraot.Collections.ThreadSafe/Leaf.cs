namespace Theraot.Collections.ThreadSafe
{
    internal class Leaf
    {
        private readonly uint _index;
        private readonly object _value;

        public Leaf(uint index, object value)
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
    }
}