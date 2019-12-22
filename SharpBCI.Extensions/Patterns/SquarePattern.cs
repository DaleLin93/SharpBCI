using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace SharpBCI.Extensions.Patterns
{

    public struct SquarePattern : ITemporalPattern
    {

        public const string V1Key = "V1";

        public const string V2Key = "V2";

        public const string FrequencyKey = "Frequency";

        [JsonProperty(V1Key)] public readonly double V1;

        [JsonProperty(V2Key)] public readonly double V2;

        [JsonProperty(FrequencyKey)] public readonly double Frequency;

        [JsonConstructor]
        public SquarePattern([JsonProperty(V1Key)] double v1, [JsonProperty(V2Key)] double v2, [JsonProperty(FrequencyKey)] double frequency)
        {
            V1 = v1;
            V2 = v2;
            Frequency = frequency;
        }

        /// <summary>
        /// format: v1,v2@frequency
        /// </summary>
        [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
        public static SquarePattern Parse(string expression)
        {
            var comma = expression.IndexOf(',');
            var at = expression.IndexOf('@', comma);
            var v1 = double.Parse(expression.Substring(0, comma));
            var v2 = double.Parse(expression.Substring(comma + 1, at - comma));
            var frequency = double.Parse(expression.Substring(0, at + 1));
            return new SquarePattern(v1, v2, frequency);
        }

        public double Sample(double t) => t * Frequency % 1 > 0.5 ? V2 : V1;

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        public override string ToString() => $"Square({V1:F2}~{V2:F2}@{Frequency:F1}Hz)";

    }

}
