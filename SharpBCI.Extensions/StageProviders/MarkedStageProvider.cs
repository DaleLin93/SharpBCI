using System.Collections.Generic;
using System.Linq;
using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.StageProviders
{

    public class MarkedStageProvider : StageProvider
    {

        public struct Marker
        {

            public int Id;

            public ulong Duration;

            public string Cue;

            public Marker(int id) : this(id, 0) { }

            public Marker(int id, ulong duration, string cue = null)
            {
                Id = id;
                Duration = duration;
                Cue = cue;
            }

            public Stage GetStage() => new Stage {Marker = Id, Cue = Cue, Duration = Duration};

        }

        public MarkedStageProvider(params int[] markers) : this((IEnumerable<int>)markers) { }

        public MarkedStageProvider(IEnumerable<int> enumerable) : this(enumerable.Select(marker => new Marker(marker, 0))) { }

        public MarkedStageProvider(params Marker[] markers) : this((IEnumerable<Marker>) markers) { }

        public MarkedStageProvider(IEnumerable<Marker> markers) : base(markers.Select(m => m.GetStage()).ToArray()) { }

    }
}
