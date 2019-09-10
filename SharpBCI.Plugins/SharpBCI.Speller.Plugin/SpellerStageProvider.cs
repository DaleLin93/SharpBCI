using System.Collections.Generic;
using System.Threading;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Experiments.Speller
{

    internal abstract class SpellerStageProvider<T> : CompositeStageProvider where T : IStageProvider
    {

        protected readonly T Provider;

        private readonly EventWaitHandle _eventWaitHandle;

        protected SpellerStageProvider(SpellerExperiment.Configuration.TestConfig testConfig, bool initialization, T provider)
            : base(
                SpellerStageProviderUtils.CreateCalibrationStages(testConfig, initialization, out var handle),
                new MarkedStageProvider(MarkerDefinitions.ExperimentStartMarker),
                new DelayStageProvider(testConfig.Trial.Interval),
                provider,
                new MarkedStageProvider(MarkerDefinitions.ExperimentEndMarker),
                new DelayStageProvider(testConfig.Trial.Interval))
        {
            Provider = provider;
            _eventWaitHandle = handle;
        }

        public void CalibrationCompleted() => _eventWaitHandle?.Set();

    }

    internal static class SpellerStageProviderUtils
    {

        public static IStageProvider CreateCalibrationStages(SpellerExperiment.Configuration.TestConfig testConfig, bool initialization, out EventWaitHandle waitHandle)
        {
            if (!initialization || testConfig.Baseline.Duration == 0)
            {
                waitHandle = null;
                return EmptyStageProvider.Instance;
            }

            /* Generating stage providers */
            var stageProviders = new LinkedList<IStageProvider>();

            /* Generating baseline stages */
            var stages = new LinkedList<Stage>();
            stages.AddLast(new Stage {Marker = MarkerDefinitions.BaselineStartMarker, Cue = "Baseline", Duration = 500});
            foreach (var stage in CountdownStageProvider.GenerateStages(testConfig.Baseline.Duration / 1000))
            {
                stage.Subtitle = stage.Cue;
                stage.Cue = "Baseline";
                stages.AddLast(stage);
            }
            var remainingMilliseconds = testConfig.Baseline.Duration % 1000;
            if (remainingMilliseconds > 0)
                stages.AddLast(new Stage {Duration = remainingMilliseconds});
            stages.AddLast(new Stage { Cue = "Calibrating", Marker = MarkerDefinitions.BaselineEndMarker});
            stageProviders.AddLast(new StageProvider(stages));

            /* waiting calibration stage provider */
            stageProviders.AddLast(new EventWaitingStageProvider(waitHandle = new ManualResetEvent(false)));
            return new CompositeStageProvider(stageProviders);
        }

    }

}
