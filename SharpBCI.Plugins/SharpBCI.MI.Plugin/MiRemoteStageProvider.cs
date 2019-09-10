using System;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Experiments.MI
{

    internal class MiRemoteStageProvider : PipelinedStageProvider
    {

        private readonly MiStimClient _miStimClient;

        public MiRemoteStageProvider(MiStimClient miStimClient) : base(1024, TimeSpan.FromMilliseconds(2))
        {
            _miStimClient = miStimClient;
            _miStimClient.StageReceived += MiStimClient_StageReceived;
            _miStimClient.Stopped += MiStimClient_Stopped;
        }

        private void MiStimClient_StageReceived(object sender, MiStage stage)
        {
            Offer(stage);
            if (stage.Duration > 0) Offer(new Stage { Duration = 0 });
        }

        private void MiStimClient_Stopped(object sender, EventArgs args) => Break();

        protected override void OnStagePolled(Stage stage)
        {
            if (stage is MiStage miStage && miStage.StimId != null)
                _miStimClient.SendStimOnSet(miStage.StimId);
        }

    }

}
