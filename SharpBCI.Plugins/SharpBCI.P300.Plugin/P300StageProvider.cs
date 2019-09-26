using SharpBCI.Core.Staging;
using System.Collections.Generic;
using SharpBCI.Extensions;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.P300
{

    internal class P300StageProvider : CompositeStageProvider
    {

        public P300StageProvider(P300Paradigm.Configuration.TestConfig testConfig)
            : base(new DelayStageProvider(testConfig.TrialInterval), new RepeatingStageProvider.Static(GenerateRepeatingStages(testConfig), testConfig.TrialCount)) { }

        public static IEnumerable<Stage> GenerateRepeatingStages(P300Paradigm.Configuration.TestConfig testConfig)
        {
            var stages = new LinkedList<Stage>();
            stages.AddLast(new Stage { Marker = MarkerDefinitions.TrialStartMarker, Duration = 0 });
            var subTrialDuration = testConfig.SubTrialDuration;
            for (int i = 0; i < testConfig.SubTrialCount; i++)
                stages.AddLast(new Stage { Marker = P300Paradigm.SubTrialMarker, Duration = subTrialDuration });
            stages.AddLast(new Stage { Marker = MarkerDefinitions.TrialEndMarker, Duration = testConfig.TrialInterval });
            return stages;
        }

    }

}
