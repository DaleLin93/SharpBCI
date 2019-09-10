using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.StageProviders
{

    public class BaselineStageProvider : MarkedStageProvider
    {

        public BaselineStageProvider(ulong duration) : this("Baseline", duration) { }

        public BaselineStageProvider(string cue, ulong duration) : base(GetMarkers(cue, duration)) { }

        public static Marker[] GetMarkers(string cue, ulong duration) => new []
        {
            new Marker(MarkerDefinitions.BaselineStartMarker, duration, cue),
            new Marker(MarkerDefinitions.BaselineEndMarker)
        };

    }

}
