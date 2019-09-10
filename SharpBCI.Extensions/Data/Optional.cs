namespace SharpBCI.Extensions.Data
{

    public sealed class Optional<T>
    {

        public Optional(bool set, T value)
        {
            Set = set;
            Value = value;
        }

        public bool Set { get; }

        public T Value { get; }

    }

}
