using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.StageProviders
{

    public class PreparationStageProvider : CompositeStageProvider
    {

        public PreparationStageProvider() : this("Preparing...", 1000, SystemVariables.PreparationCountdown.Get(SystemVariables.Context)) { }

        public PreparationStageProvider(ulong? delayMillis, uint? countdownSeconds) : this("Preparing...", delayMillis ?? 1000, countdownSeconds ?? SystemVariables.PreparationCountdown.Get(SystemVariables.Context)) { }

        public PreparationStageProvider(string cue, ulong delayMillis, uint countdownSeconds) : base(new DelayStageProvider(cue, delayMillis), new CountdownStageProvider(countdownSeconds)) { }

    }

}
