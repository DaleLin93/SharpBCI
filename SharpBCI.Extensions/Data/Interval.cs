using System;
using Newtonsoft.Json;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.Extensions.Data
{

    [Presenter(typeof(IntervalPresenter))]
    public struct Interval
    {

        private const string MinKey = "Min";

        private const string MaxKey = "Max";

        [JsonConstructor]
        public Interval([JsonProperty(MinKey)] double a, [JsonProperty(MaxKey)] double b)
        {
            MinValue = Math.Min(a, b);
            MaxValue = Math.Max(a, b);
        }

        [JsonIgnore] public double Length => MaxValue - MinValue;

        [JsonProperty(MinKey)] public double MinValue { get; }

        [JsonProperty(MaxKey)] public double MaxValue { get; }

    }

}
