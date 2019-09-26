using System;
using System.Collections.Generic;
using MarukoLib.Lang;

namespace SharpBCI.Paradigms.Speller
{

    public enum SpellerControlParadigm
    {
        EyeTracking, SsvepWithEyeTracking, P300WithEyeTracking
    }

    public static class SpellerParadigmExt
    {

        private static readonly IDictionary<string, SpellerControlParadigm> Paradigms = new Dictionary<string, SpellerControlParadigm>();

        static SpellerParadigmExt()
        {
            foreach (var value in Enum.GetValues(typeof(SpellerControlParadigm)))
            {
                var paradigm  = (SpellerControlParadigm)value;
                Paradigms[paradigm.GetName()] = paradigm;
            }
        }

        public static readonly TypeConverter TypeConverter = TypeConverter.Of<SpellerControlParadigm, string>(p => p.GetName(), s => Paradigms[s]);

        public static string GetName(this SpellerControlParadigm controlParadigm) => controlParadigm.ToString().Replace("With", " + ");

    }

}