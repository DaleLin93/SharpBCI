using SharpBCI.Core.Staging;
using System;
using System.Collections.Generic;
using SharpBCI.Extensions;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Experiments.MRCP
{

    internal class MrcpStage : Stage
    {

        /// <summary>
        /// Only initial stage has non-null value.
        /// </summary>
        public int? LiftAt;

        /// <summary>
        /// Range of [0, TotalTicks).
        /// </summary>
        public int CurrentTick;

        public int TotalTicks;

        public bool IsInitialStage => LiftAt != null;

    }

    internal class MrcpStageProvider : SegmentedStageProvider
    {

        private const int TickLengthInMilliseconds = 100;

        private const int TicksPerSecond = 1000 / TickLengthInMilliseconds;

        private readonly Random _r = new Random();

        private readonly MrcpExperiment.Configuration.TestConfig _testConfig;

        private long _remaining;

        public MrcpStageProvider(MrcpExperiment.Configuration.TestConfig testConfig) : base(true)
        {
            _testConfig = testConfig;
            _remaining = _testConfig.TrialCount;
        }

        protected override IEnumerable<Stage> Following()
        {
            if (_remaining <= 0) return null;
            var liftAt = _r.Next(3, 7) * 10;
            var tickNum = TicksPerSecond * _testConfig.TrialDuration;
            var stages = new Stage[tickNum + 1];
            for (var i = 0; i < tickNum; i++)
            {
                int? marker = null;
                if (i == 0)
                    marker = MarkerDefinitions.TrialStartMarker;
                else if (i == liftAt)
                    marker = MrcpExperiment.LiftMarker;
                stages[i] = new MrcpStage
                {
                    LiftAt = i == 0 ? (int?) liftAt : null,
                    CurrentTick = i,
                    TotalTicks = tickNum,
                    Marker = marker,
                    Duration = TickLengthInMilliseconds
                };
            }
            stages[tickNum] = new Stage {Marker = MarkerDefinitions.TrialEndMarker, Duration = (ulong) (_testConfig.InterStimulusInterval * 1000)};
            _remaining--;
            return stages;
        }

    }

}
