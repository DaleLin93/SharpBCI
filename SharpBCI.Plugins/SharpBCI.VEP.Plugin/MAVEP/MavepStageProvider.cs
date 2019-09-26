using System.Collections.Generic;
using MarukoLib.Lang;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.VEP.MAVEP
{

    internal class MavepStageProvider : SegmentedStageProvider
    {

        private readonly IBoolSequence _randomBools = new RandomBools();

        private readonly MavepParadigm.Configuration.TestConfig _testConfig;

        private ulong _remainingTrialCount;

        public MavepStageProvider(MavepParadigm.Configuration.TestConfig testConfig) : base(true)
        {
            _testConfig = testConfig;

            _remainingTrialCount = testConfig.TrialCount;
        }

        protected override IEnumerable<Stage> Following()
        {
            if (_remainingTrialCount <= 0) return null;
            var value = _randomBools.Next(); // false - 0, left-right; true - 1, right-left; 
            var start = new Stage {Marker = MarkerDefinitions.TrialStartMarker};
            var left = new Stage {Marker = MavepParadigm.LeftStimMarker, Duration = 25};
            var right = new Stage {Marker = MavepParadigm.RightStimMarker, Duration = 25};
            var blank = new Stage {Marker = MavepParadigm.StimClearMarker, Duration = 75};
            var end = new Stage {Marker = MarkerDefinitions.TrialEndMarker};
            var interval = new Stage {Marker = MavepParadigm.StimClearMarker, Duration = _testConfig.InterStimulusInterval};
            var first = value ? right : left;
            var second = value ? left : right;
            var stages = new[] {start, first, blank, second, blank, end, interval};
            _remainingTrialCount--;
            return stages;
        }

    }

}
