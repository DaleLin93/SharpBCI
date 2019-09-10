using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.StageProviders
{

    public class DelayStageProvider : StageProvider
    {

        public DelayStageProvider(ulong milliseconds) : this("", milliseconds) { }

        public DelayStageProvider(string cue, ulong milliseconds) : base(new Stage { Identifier = "Delay" + milliseconds, Cue = cue, Duration = milliseconds }) { }

    }

}
