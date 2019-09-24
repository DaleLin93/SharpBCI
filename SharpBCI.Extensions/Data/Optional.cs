using Newtonsoft.Json;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.Extensions.Data
{

    [Presenter(typeof(OptionalPresenter))]
    public struct Optional<T>
    {

        private const string HasKey = "Has";

        private const string ValueKey = "Value";

        public Optional(T value) : this(true, value) { }

        [JsonConstructor]
        public Optional([JsonProperty(HasKey)] bool has, [JsonProperty(ValueKey)] T value)
        {
            Has = has;
            Value = value;
        }

        [JsonProperty(HasKey)] public bool Has { get; }

        [JsonProperty(ValueKey)] public T Value { get; }

    }

}
