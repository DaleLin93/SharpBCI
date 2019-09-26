using SharpBCI.Core.Staging;
using System.Collections.Generic;
using SharpBCI.Extensions;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.VEP.SSVEP
{

    internal class SsvepStageProvider : SegmentedStageProvider
    {

        private readonly SsvepParadigm.Configuration.TestConfig _testConfig;

        private ulong _remainingTrialCount;

        public SsvepStageProvider(SsvepParadigm.Configuration.TestConfig testConfig) : base(true)
        {
            _testConfig = testConfig;

            _remainingTrialCount = testConfig.TrialCount;
        }

        protected override IEnumerable<Stage> Following()
        {
            if (_remainingTrialCount <= 0)
                return null;
            var stages = new[]
            {
                new Stage {Marker = MarkerDefinitions.TrialStartMarker, Duration = _testConfig.TrialDuration},
                new Stage {Marker = MarkerDefinitions.TrialEndMarker, Duration = _testConfig.InterStimulusInterval},
            };
            _remainingTrialCount--;
            return stages;
        }

    }

}
