using System;
using SharpBCI.Core.Staging;
using System.Collections.Generic;
using SharpBCI.Extensions;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.Speller.P300
{

    internal class P300StaticSpellerStageProvider : SpellerStageProvider<RepeatingStageProvider>
    {

        public P300StaticSpellerStageProvider(SpellerController spellerController, SpellerParadigm.Configuration.TestConfig testConfig)
            : base(testConfig, false, RepeatingStageProvider.Static.Unlimited(P300SpellerStageProviderUtils.GenerateRepeatingStages(testConfig)))
        {
            spellerController.Calibrated += (sender, e) => CalibrationCompleted();
            spellerController.Stopping += (sender, e) => ((RepeatingStageProvider)this[1]).Break();
        }

    }

    internal class P300DynamicSpellerStageProvider : SpellerStageProvider<PipelinedStageProvider>
    {

        public P300DynamicSpellerStageProvider(SpellerController spellerController, SpellerParadigm.Configuration.TestConfig testConfig)
            : base(testConfig, false, new PipelinedStageProvider((int)(testConfig.SubTrialCount * 2), TimeSpan.FromMilliseconds(5)))
        {
            spellerController.Calibrated += (sender, e) => CalibrationCompleted();
            spellerController.Stopping += (sender, e) => Provider.Break();
            spellerController.CreatingTrial += (sender, e) => Provider.Offer(P300SpellerStageProviderUtils.GenerateRepeatingStages(testConfig));
        }

    }

    internal static class P300SpellerStageProviderUtils
    {

        public static IStageProvider GetParadigmProvider(SpellerController spellerController,
            SpellerParadigm.Configuration.TestConfig testConfig)
        {
            return testConfig.DynamicInterval
                ? (IStageProvider)new P300DynamicSpellerStageProvider(spellerController, testConfig)
                : new P300StaticSpellerStageProvider(spellerController, testConfig);
        }

        public static IReadOnlyCollection<Stage> GenerateRepeatingStages(SpellerParadigm.Configuration.TestConfig testConfig)
        {
            var stages = new LinkedList<Stage>();
            stages.AddLast(new Stage { Marker = MarkerDefinitions.TrialStartMarker, Duration = 0 });
            var subTrialDuration = testConfig.SubTrialDuration;
            for (var i = 0; i < testConfig.SubTrialCount; i++)
                stages.AddLast(new Stage { Marker = SpellerMarkerDefinitions.SubTrialMarker, Duration = subTrialDuration });
            stages.AddLast(new Stage {Marker = MarkerDefinitions.TrialEndMarker, Duration = testConfig.DynamicInterval ? 0 : testConfig.Trial.Interval});
            return stages;
        }

    }

}
