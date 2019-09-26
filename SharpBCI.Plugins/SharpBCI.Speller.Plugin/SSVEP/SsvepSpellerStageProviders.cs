using System;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.Speller.SSVEP
{

    internal class SsvepStaticSpellerStageProvider : SpellerStageProvider<RepeatingStageProvider>
    {

        public SsvepStaticSpellerStageProvider(SpellerController spellerController, SpellerParadigm.Configuration.TestConfig testConfig)
            : base(testConfig, true, RepeatingStageProvider.Static.Unlimited(SsvepSpellerStageProviderUtils.GenerateRepeatingStages(testConfig)))
        {
            spellerController.Calibrated += (sender, e) => CalibrationCompleted();
            spellerController.Stopping += (sender, e) => Provider.Break();
        }

    }

    internal class SsvepDynamicSpellerStageProvider : SpellerStageProvider<PipelinedStageProvider>
    {

        public SsvepDynamicSpellerStageProvider(SpellerController spellerController, SpellerParadigm.Configuration.TestConfig testConfig)
            : base(testConfig, true, new PipelinedStageProvider(16, TimeSpan.FromMilliseconds(5)))
        {
            spellerController.Calibrated += (sender, e) => CalibrationCompleted();
            spellerController.Stopping += (sender, e) => Provider.Break();
            spellerController.CreatingTrial += (sender, e) => Provider.Offer(SsvepSpellerStageProviderUtils.GenerateRepeatingStages(testConfig));
        }

    }

    internal static class SsvepSpellerStageProviderUtils
    {

        public static IStageProvider GetParadigmProvider(SpellerController spellerController,
            SpellerParadigm.Configuration.TestConfig testConfig)
        {
            return testConfig.DynamicInterval
                ? (IStageProvider)new SsvepDynamicSpellerStageProvider(spellerController, testConfig)
                : new SsvepStaticSpellerStageProvider(spellerController, testConfig);
        }

        public static Stage[] GenerateRepeatingStages(SpellerParadigm.Configuration.TestConfig testConfig) => new[]
        {
            new Stage { Marker = MarkerDefinitions.TrialStartMarker, Duration = testConfig.Trial.Duration},
            new Stage {Marker = MarkerDefinitions.TrialEndMarker, Duration = testConfig.DynamicInterval ? 0 : testConfig.Trial.Interval},
        };

    }

}
