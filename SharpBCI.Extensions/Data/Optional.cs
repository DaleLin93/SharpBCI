using Newtonsoft.Json;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.Extensions.Data
{

    [Presenter(typeof(OptionalPresenter))]
    public struct Optional<T>
    {

        private const string HasValueKey = nameof(HasValue);

        private const string ValueKey = nameof(Value);

        public Optional(T value) : this(true, value) { }

        [JsonConstructor]
        public Optional([JsonProperty(HasValueKey)] bool hasValue, [JsonProperty(ValueKey)] T value)
        {
            HasValue = hasValue;
            Value = value;
        }

        [JsonProperty(HasValueKey)] public bool HasValue { get; }

        [JsonProperty(ValueKey)] public T Value { get; }

    }

}
