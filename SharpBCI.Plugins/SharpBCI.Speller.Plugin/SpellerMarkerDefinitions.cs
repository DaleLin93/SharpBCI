using SharpBCI.Extensions;

namespace SharpBCI.Experiments.Speller
{

    public static class SpellerMarkerDefinitions
    {

        [MarkerDefinition("speller:p300")]
        public const int SubTrialMarker = MarkerDefinitions.CustomMarkerBase + 1;

    }

}
