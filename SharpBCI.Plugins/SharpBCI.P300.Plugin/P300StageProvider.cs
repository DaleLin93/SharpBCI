using SharpBCI.Core.Staging;
using System.Collections.Generic;
using SharpBCI.Extensions;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Experiments.P300
{

    internal class P300StageProvider : CompositeStageProvider
    {

        public P300StageProvider(P300Experiment.Configuration.TestConfig testConfig)
            : base(new DelayStageProvider(testConfig.TrialInterval), new RepeatingStageProvider.Static(GenerateRepeatingStages(testConfig), testConfig.TrialCount)) { }

        public static IEnumerable<Stage> GenerateRepeatingStages(P300Experiment.Configuration.TestConfig testConfig)
        {
            var stages = new LinkedList<Stage>();
            stages.AddLast(new Stage { Marker = MarkerDefinitions.TrialStartMarker, Duration = 0 });
            var subTrialDuration = testConfig.SubTrialDuration;
            for (int i = 0; i < testConfig.SubTrialCount; i++)
                stages.AddLast(new Stage { Marker = P300Experiment.SubTrialMarker, Duration = subTrialDuration });
            stages.AddLast(new Stage { Marker = MarkerDefinitions.TrialEndMarker, Duration = testConfig.TrialInterval });
            return stages;
        }

    }

}
