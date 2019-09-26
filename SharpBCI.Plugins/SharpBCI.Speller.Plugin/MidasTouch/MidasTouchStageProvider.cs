using SharpBCI.Core.Staging;
using System.Collections.Generic;
using MarukoLib.Lang;
using SharpBCI.Extensions;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.Speller.MidasTouch
{

    internal class MidasTouchStageProvider : SegmentedStageProvider
    {

        private readonly MidasTouchParadigm.Configuration.TestConfig _testConfig;

        private readonly IRandomBoolSequence _randomBoolSequence;

        private ulong _remainingTrialCount;

        public MidasTouchStageProvider(MidasTouchParadigm.Configuration.TestConfig testConfig) : base(true)
        {
            _testConfig = testConfig;
            _randomBoolSequence = testConfig.TargetRate.CreateRandomBoolSequence();
            _remainingTrialCount = testConfig.TrialCount;
        }

        protected override IEnumerable<Stage> Following()
        {
            if (_remainingTrialCount <= 0)
                return null;
            var target = _randomBoolSequence.Next();
            var stages = new[]
            {
                new Stage {Cue = target ? "YES" : "NO", Marker = MarkerDefinitions.TrialStartMarker, Duration = _testConfig.TrialDuration, Tag = target},
                new Stage {Marker = MarkerDefinitions.TrialEndMarker, Duration = _testConfig.InterStimulusInterval},
            };
            _remainingTrialCount--;
            return stages;
        }

    }

}
