using SharpBCI.Extensions;

namespace SharpBCI.Paradigms.Speller
{

    public static class SpellerMarkerDefinitions
    {

        [Marker("speller:p300")]
        public const int SubTrialMarker = MarkerDefinitions.CustomMarkerBase + 1;

    }

}
