using System;
using System.Collections.Generic;
using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.StageProviders
{

    public class CountdownStageProvider : StageProvider
    {

        public CountdownStageProvider(uint secs, uint numberDuration = 700) : base(GenerateStages(secs, numberDuration)) { }

        public static ICollection<Stage> GenerateStages(uint secs, uint numberDuration = 1000)
        {
            numberDuration = Math.Max(0, Math.Min(numberDuration, 1000));
            var blankDuration = 1000 - numberDuration;
            var stages = new List<Stage>((int)(secs * 2));
            for (var i = 0; i < secs; i++)
            {
                var secsRemaining = (secs - i);
                if (numberDuration > 0)
                    stages.Add(new Stage {Identifier = "Countdown" + secsRemaining, Cue = secsRemaining.ToString(), Duration = numberDuration});
                if (blankDuration > 0)
                    stages.Add(new Stage {Identifier = "CountdownBlank" + secsRemaining, Cue = "", Duration = blankDuration});
            }
            return stages;
        }

    }

}
