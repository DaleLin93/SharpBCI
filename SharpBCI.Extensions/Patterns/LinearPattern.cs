using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace SharpBCI.Extensions.Patterns
{

    public struct LinearPattern : ITemporalPattern
    {

        public const string V1Key = "V1";

        public const string V2Key = "V2";

        public const string FrequencyKey = "Frequency";

        [JsonProperty(V1Key)] public readonly double V1;

        [JsonProperty(V2Key)] public readonly double V2;

        [JsonProperty(FrequencyKey)] public readonly double Frequency;

        [JsonConstructor]
        public LinearPattern([JsonProperty(V1Key)] double v1, [JsonProperty(V2Key)] double v2, [JsonProperty(FrequencyKey)] double frequency)
        {
            V1 = v1;
            V2 = v2;
            Frequency = frequency;
        }

        /// <summary>
        /// format: v1,v2@frequency
        /// </summary>
        [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
        public static LinearPattern Parse(string expression)
        {
            var comma = expression.IndexOf(',');
            var at = expression.IndexOf('@', comma);
            var v1 = double.Parse(expression.Substring(0, comma));
            var v2 = double.Parse(expression.Substring(comma + 1, at - comma));
            var frequency = double.Parse(expression.Substring(0, at + 1));
            return new LinearPattern(v1, v2, frequency);
        }

        public double Sample(double t)
        {
            var pt = t * Frequency % 1;
            return pt > 0.5 ? V2 - (V2 - V1) * (pt - 0.5) : V1 + (V2 - V1) * pt;
        }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        public override string ToString() => $"Linear({V1:F2}~{V2:F2}@{Frequency:F1}Hz)";

    }

}
