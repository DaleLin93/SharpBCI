using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace SharpBCI.Extensions.Patterns
{

    public struct TimeVaryingSinusoidalPattern : ITemporalPattern
    {

        public const string FrequencyKey = "Frequency";

        public const string DeltaKey = "Delta";

        public const string PhaseKey = "Phase";

        [JsonProperty(FrequencyKey)] public readonly double Frequency;

        [JsonProperty(DeltaKey)] public readonly double Delta;

        [JsonProperty(PhaseKey)] public readonly double Phase;

        [JsonConstructor]
        public TimeVaryingSinusoidalPattern([JsonProperty(FrequencyKey)] double frequency,
            [JsonProperty(FrequencyKey)] double delta, [JsonProperty(PhaseKey)] double phase = 0)
        {
            Frequency = frequency;
            Delta = delta;
            Phase = phase;
        }

        [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
        public static TimeVaryingSinusoidalPattern Parse(string expression)
        {
            var idxTilde = expression.IndexOf('~');
            var idxAt = expression.IndexOf('@');
            if (idxTilde > 0 && idxAt > idxTilde) throw new ArgumentException($"Malformed time-varying sinusoidal pattern: '{expression}'");
            var frequency = double.Parse(idxAt < 0 ? expression : expression.Substring(0, idxAt));
            var phase = idxAt < 0 ? 0 : double.Parse(expression.Substring(idxAt + 1, expression.Length - (idxAt < 0 ? idxTilde : idxAt + 1)));
            var delta = idxTilde < 0 ? 0 : double.Parse(expression.Substring(idxTilde + 1));
            return new TimeVaryingSinusoidalPattern(frequency, delta, phase);
        }

        public double Sample(double t)
        {
            var endFrequency = Frequency + t * Delta;
            var phase = Phase + (Frequency + endFrequency) * t;
            return Math.Sin(phase * Math.PI);
        }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        public override string ToString() => Phase != 0 ? $"Time-varying Sin({Frequency:F1}Hz@{Phase:F1}π)" : $"Sin({Frequency:F1}Hz)";

    }

    public struct TimeVaryingCosinusoidalPattern : ITemporalPattern
    {

        public const string FrequencyKey = "Frequency";

        public const string DeltaKey = "Delta";

        public const string PhaseKey = "Phase";

        [JsonProperty(FrequencyKey)] public readonly double Frequency;

        [JsonProperty(DeltaKey)] public readonly double Delta;

        [JsonProperty(PhaseKey)] public readonly double Phase;

        [JsonConstructor]
        public TimeVaryingCosinusoidalPattern([JsonProperty(FrequencyKey)] double frequency,
            [JsonProperty(FrequencyKey)] double delta, [JsonProperty(PhaseKey)] double phase = 0)
        {
            Frequency = frequency;
            Delta = delta;
            Phase = phase;
        }

        [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
        public static TimeVaryingCosinusoidalPattern Parse(string expression)
        {
            var idxTilde = expression.IndexOf('~');
            var idxAt = expression.IndexOf('@');
            if (idxTilde > 0 && idxAt > idxTilde) throw new ArgumentException($"Malformed time-varying cosinusoidal pattern: '{expression}'");
            var frequency = double.Parse(idxAt < 0 ? expression : expression.Substring(0, idxAt));
            var phase = idxAt < 0 ? 0 : double.Parse(expression.Substring(idxAt + 1, expression.Length - (idxAt < 0 ? idxTilde : idxAt + 1)));
            var delta = idxTilde < 0 ? 0 : double.Parse(expression.Substring(idxTilde + 1));
            return new TimeVaryingCosinusoidalPattern(frequency, delta, phase);
        }

        public double Sample(double t)
        {
            var endFrequency = Frequency + t * Delta;
            var phase = Phase + (Frequency + endFrequency) * t;
            return Math.Cos(phase * Math.PI);
        }

        [SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator")]
        public override string ToString() => Phase != 0 ? $"Time-varying Cos({Frequency:F1}Hz@{Phase:F1}π)" : $"Sin({Frequency:F1}Hz)";

    }

}
