using System;
using System.Collections.Generic;
using System.Linq;
using MarukoLib.Lang;
using Newtonsoft.Json;

namespace SharpBCI.Extensions.Patterns
{

    public interface ITemporalPattern : IPattern<double, double> { }

    public class CompositeTemporalPattern : CompositeTemporalPattern<ITemporalPattern>
    {

        [JsonConstructor]
        public CompositeTemporalPattern([JsonProperty(PatternsKey)] IReadOnlyCollection<ITemporalPattern> patterns) : base(patterns) { }

    }

    public class CompositeTemporalPattern<T> : ITemporalPattern, ICompositePattern<T> where T : ITemporalPattern
    {

        public const string PatternsKey = "Patterns";

        [JsonProperty(PatternsKey)] public readonly IReadOnlyCollection<T> Patterns;

        private string _cachedToString;

        [JsonConstructor]
        public CompositeTemporalPattern([JsonProperty(PatternsKey)] IReadOnlyCollection<T> patterns)
        {
            if (patterns.Count == 0) throw new ArgumentException("no patterns");
            if (patterns.Any(Functions.IsNull)) throw new ArgumentException("pattern cannot be null");
            Patterns = patterns;
        }

        public CompositeTemporalPattern(params T[] patterns) : this((IReadOnlyCollection<T>)patterns) { }

        public double Sample(double timePoint) => Patterns.Sample(timePoint);

        public override string ToString() => _cachedToString ?? (_cachedToString = Patterns.Join(",", pattern => pattern.ToString()));

        IReadOnlyCollection<T> ICompositePattern<T>.Patterns => Patterns;

    }

    public static class TemporalPatternExt
    {

        public static double Sample<T>(this IEnumerable<T> patterns, double t) where T : ITemporalPattern
        {
            decimal result = 0;
            uint count = 0;
            foreach (var pattern in patterns)
            {
                result += (decimal)pattern.Sample(t);
                count++;
            }
            return (double)(result / count);
        }

    }

}
