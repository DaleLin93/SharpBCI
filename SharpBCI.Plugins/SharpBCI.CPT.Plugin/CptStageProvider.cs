using SharpBCI.Core.Staging;
using System;
using System.Collections.Generic;
using System.Linq;
using SharpBCI.Extensions.StageProviders;
using MarukoLib.Lang.Sequence;

namespace SharpBCI.Paradigms.CPT
{

    internal class CptStage : Stage
    {

        public bool IsTarget { get; set; }

    }

    internal class CptStageProvider : SegmentedStageProvider
    {

        private const string NonTargetChars = "ABCDEFGHIJKLMNOPQRSTUVWYZ";

        private readonly Random _r = new Random();

        private readonly CptParadigm.Configuration.TestConfig _testConfig;

        private readonly IRandomBools _randomBoolSequence;

        private ulong _remaining;

        private bool _completed;

        public CptStageProvider(CptParadigm.Configuration.TestConfig testConfig) : base(true)
        {
            _testConfig = testConfig;
            _randomBoolSequence = testConfig.TargetRate.CreateRandomBoolSequence();
            _remaining = testConfig.TotalDuration;
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
            var marker = target ? CptParadigm.TargetDisplayMarker : CptParadigm.NonTargetDisplayMarker;
            stages[0] = new CptStage {Cue = cue, IsTarget = target, Duration = _testConfig.LetterDuration, Marker = marker};
            stages[1] = new Stage {Cue = "", Duration = _testConfig.InterStimulusInterval, Marker = CptParadigm.IntervalMarker};
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
