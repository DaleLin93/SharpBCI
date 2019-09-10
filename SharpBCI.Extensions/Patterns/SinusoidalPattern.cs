using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace SharpBCI.Extensions.Patterns
{

    public struct SinusoidalPattern : ITemporalPattern
    {

        public const string FrequencyKey = "Frequency";

        public const string PhaseKey = "Phase";

        [JsonProperty(FrequencyKey)] public readonly double Frequency;

        [JsonProperty(PhaseKey)] public readonly double Phase;

        [JsonConstructor]
        public SinusoidalPattern([JsonProperty(FrequencyKey)] double frequency, [JsonProperty(PhaseKey)] double phase = 0)
        {
            Frequency = frequency;
            Phase = phase;
        }

        [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
        public static SinusoidalPattern Parse(string expression)
        {
            var at = expression.IndexOf('@');
            var frequency = double.Parse(at < 0 ? expression : expression.Substring(0, at));
            var phase = at < 0 ? 0 : double.Parse(expression.Substring(at + 1));
            return new SinusoidalPattern(frequency, phase);
        }

        //(-Math.Cos((Phase + Frequency * t * 2) * Math.PI) + 1) / 2;
        public double Sample(double t) => Math.Sin((Phase + Frequency * t * 2) * Math.PI);

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        public override string ToString() => Phase != 0 ? $"Sin({Frequency:F1}Hz@{Phase:F1}π)" : $"Sin({Frequency:F1}Hz)";

    }

    public struct CosinusoidalPattern : ITemporalPattern
    {

        public const string FrequencyKey = "Frequency";

        public const string PhaseKey = "Phase";

        [JsonProperty(FrequencyKey)] public readonly double Frequency;

        [JsonProperty(PhaseKey)] public readonly double Phase;

        [JsonConstructor]
        public CosinusoidalPattern([JsonProperty(FrequencyKey)] double frequency, [JsonProperty(PhaseKey)] double phase = 0)
        {
            Frequency = frequency;
            Phase = phase;
        }

        [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
        public static CosinusoidalPattern Parse(string expression)
        {
            var at = expression.IndexOf('@');
            var frequency = double.Parse(at < 0 ? expression : expression.Substring(0, at));
            var phase = at < 0 ? 0 : double.Parse(expression.Substring(at + 1));
            return new CosinusoidalPattern(frequency, phase);
        }

        public double Sample(double t) => Math.Cos((Phase + Frequency * t * 2) * Math.PI);

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        public override string ToString() => Phase != 0 ? $"Cos({Frequency:F1}Hz@{Phase:F1}π)" : $"Cos({Frequency:F1}Hz)";

    }

}
