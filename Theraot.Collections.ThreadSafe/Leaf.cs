namespace Theraot.Collections.ThreadSafe
{
    internal struct Leaf : INode
    {
        private readonly object _value;

        public Leaf(object value)
        {
            _value = value;
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