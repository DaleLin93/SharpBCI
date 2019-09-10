using System;
using System.Diagnostics.CodeAnalysis;

namespace SharpBCI.Experiments.Speller
{

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    internal static class SpellerUtils
    {

        /// <summary>
        /// Compute BCI utility under specific time unit (U in bits/time unit).
        /// </summary>
        public static double BCIUtility(double N, double P, double time) => ByTime(BCIUtility(N, P), time); 

        /// <summary>
        /// Compute BCI utility (U in bits/segment).
        /// </summary>
        /// <param name="N">The number of possible selection.</param>
        /// <param name="P">The correct choice probability(estimated accuracy).</param>
        /// <returns> U = (2P-1) * log2(N-1) </returns>
        public static double BCIUtility(double N, double P) => (2 * P - 1) * Math.Log(N - 1, 2); 

        /// <summary>
        /// Compute information transfer rate under specific time unit (ITR in bits/time unit).
        /// </summary>
        public static double ITR(double N, double P, double time) => ByTime(ITR(N, P), time);

        /// <summary>
        /// Compute information transfer rate (ITR in bits/segment).
        /// </summary>
        /// <param name="N">The number of possible selection.</param>
        /// <param name="P">The correct choice probability(estimated accuracy).</param>
        /// <returns> ITR = log2(N) + log2(P)P + (1-P)log2((1-P)/(N-1)) </returns>
        public static double ITR(double N, double P) => Math.Log(N, 2) + P * Math.Log(P, 2) + (1 - P) * Math.Log((1 - P) / (N - 1), 2);

        public static double ByTime(double val, double time) => val / time;

    }
}
