using System;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.Speller.EyeTracking
{

    internal class EyeTrackingStaticSpellerStageProvider : SpellerStageProvider<RepeatingStageProvider>
    {

        public EyeTrackingStaticSpellerStageProvider(SpellerController spellerController, SpellerParadigm.Configuration.TestConfig testConfig)
            : base(testConfig, false, RepeatingStageProvider.Static.Unlimited(EyeTrackingSpellerStageProviderUtils.GenerateRepeatingStages(testConfig)))
        {
            spellerController.Calibrated += (sender, e) => CalibrationCompleted();
            spellerController.Stopping += (sender, e) => Provider.Break();
        }

    }

    internal class EyeTrackingDynamicSpellerStageProvider : SpellerStageProvider<PipelinedStageProvider>
    {

        public EyeTrackingDynamicSpellerStageProvider(SpellerController spellerController, SpellerParadigm.Configuration.TestConfig testConfig)
            : base(testConfig, false, new PipelinedStageProvider(1024, TimeSpan.FromMilliseconds(5)))
        {
            spellerController.Stopping += (sender, e) => Provider.Break();
            spellerController.CreatingTrial += (sender, e) => Provider.Offer(EyeTrackingSpellerStageProviderUtils.GenerateRepeatingStages(testConfig));
        }

    }

    internal static class EyeTrackingSpellerStageProviderUtils
    {

        public static IStageProvider GetParadigmProvider(SpellerController spellerController, 
            SpellerParadigm.Configuration.TestConfig testConfig) => testConfig.DynamicInterval
                ? (IStageProvider) new EyeTrackingDynamicSpellerStageProvider(spellerController, testConfig)
                : new EyeTrackingStaticSpellerStageProvider(spellerController, testConfig);

        public static Stage[] GenerateRepeatingStages(SpellerParadigm.Configuration.TestConfig testConfig) => new[]
        {
            new Stage { Marker = MarkerDefinitions.TrialStartMarker, Duration = testConfig.Trial.Duration},
            new Stage { Marker = MarkerDefinitions.TrialEndMarker, Duration = testConfig.DynamicInterval ? 0 : testConfig.Trial.Interval},
        };

    }

}
