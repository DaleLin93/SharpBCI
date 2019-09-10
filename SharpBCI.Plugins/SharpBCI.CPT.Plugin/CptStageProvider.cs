using SharpBCI.Core.Staging;
using System;
using System.Collections.Generic;
using System.Linq;
using MarukoLib.Lang;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Experiments.CPT
{

    internal class CptStage : Stage
    {

        public bool IsTarget { get; set; }

    }

    internal class CptStageProvider : SegmentedStageProvider
    {

        private const string NonTargetChars = "ABCDEFGHIJKLMNOPQRSTUVWYZ";

        private readonly Random _r = new Random();

        private readonly CptExperiment.Configuration.TestConfig _testConfig;

        private readonly IRandomBoolSequence _randomBoolSequence;

        private ulong _remaining;

        private bool _completed;

        public CptStageProvider(CptExperiment.Configuration.TestConfig testConfig) : base(true)
        {
            _testConfig = testConfig;
            _randomBoolSequence = testConfig.PseudoRandom
                ? (IRandomBoolSequence) new PseudoRandom(testConfig.TargetRate) : new RandomBools(testConfig.TargetRate);
            _remaining = testConfig.ExperimentDuration;
        }

        protected override IEnumerable<Stage> Following()
        {
            if (_completed)
                return null;
            var target = _randomBoolSequence.Next();
            var cue = target ? NonTargetChars.ElementAt(_r.Next(NonTargetChars.Length)).ToString() : "X";
            if (_testConfig.Still)
            {
                var stage = new Stage { Cue = cue, Duration = _remaining };
                _completed = true;
                return new[] { stage };
            }

            var stages = new Stage[2];
            var marker = target ? CptExperiment.TargetDisplayMarker : CptExperiment.NonTargetDisplayMarker;
            stages[0] = new CptStage {Cue = cue, IsTarget = target, Duration = _testConfig.LetterDuration, Marker = marker};
            stages[1] = new Stage {Cue = "", Duration = _testConfig.InterStimulusInterval, Marker = CptExperiment.IntervalMarker};
            if (_remaining < _testConfig.LetterDuration + _testConfig.InterStimulusInterval)
                _completed = true;
            else
            {
                _remaining -= _testConfig.LetterDuration + _testConfig.InterStimulusInterval;
                if (_remaining < _testConfig.LetterDuration)
                    _completed = true;
            }
            return stages;
        }

    }

}
