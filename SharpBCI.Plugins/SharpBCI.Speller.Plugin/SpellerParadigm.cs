using System;
using System.Collections.Generic;
using MarukoLib.Lang;

namespace SharpBCI.Experiments.Speller
{

    public enum SpellerParadigm
    {
        EyeTracking, SsvepWithEyeTracking, P300WithEyeTracking
    }

    public static class SpellerParadigmExt
    {

        private static readonly IDictionary<string, SpellerParadigm> Paradigms = new Dictionary<string, SpellerParadigm>();

        static SpellerParadigmExt()
        {
            foreach (var value in Enum.GetValues(typeof(SpellerParadigm)))
            {
                var paradigm  = (SpellerParadigm)value;
                Paradigms[paradigm.GetName()] = paradigm;
            }
        }

        public static readonly TypeConverter TypeConverter = TypeConverter.Of<SpellerParadigm, string>(p => p.GetName(), s => Paradigms[s]);

        public static string GetName(this SpellerParadigm paradigm) => paradigm.ToString().Replace("With", " + ");

    }

}