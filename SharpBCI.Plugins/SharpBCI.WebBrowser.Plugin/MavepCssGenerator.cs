using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SharpBCI.Experiments.WebBrowser
{
    public class MavepCssGenerator
    {

        public const uint VisualStimLasting = 25;

        public const uint VisualStimInterval = 75;

        public MavepCssGenerator(int bitCount)
        {
            if (bitCount > 60) throw new ArgumentException();
            BitCount = (byte)bitCount;
        }

        public byte BitCount { get; }

        public uint StimulationDuration => BitCount * (VisualStimLasting + VisualStimInterval);

        public ulong MaxCommandCount => (ulong)(1 << BitCount);

        public void GetStyle(ulong code, out string left, out string right)
        {
            if (code >= MaxCommandCount) throw new ArgumentOutOfRangeException();
            var seq = new LinkedList<KeyValuePair<bool?, uint>>(); // KeyValuePair<left?, duration>
            for (var i = 0; i < BitCount; i++)
            {
                var flag = (code & (ulong)(1 << (BitCount - i))) != 0;
                seq.AddLast(new KeyValuePair<bool?, uint>(flag, VisualStimLasting));
                seq.AddLast(new KeyValuePair<bool?, uint>(null, VisualStimInterval));
                seq.AddLast(new KeyValuePair<bool?, uint>(!flag, VisualStimLasting));
                seq.AddLast(new KeyValuePair<bool?, uint>(null, VisualStimInterval));
            }
            var totalTime = seq.Sum(pair => pair.Value);

            var styles = new string[2];
            var stringBuilder = new StringBuilder(128);
            for (var i = 0; i < 2; i++)
            {
                stringBuilder.Clear();
                var currentTime = (uint)0;
                var isLeft = i == 0;
                stringBuilder.Append('{');
                foreach (var pair in seq)
                {
                    var percentage = currentTime / (double)totalTime;
                    stringBuilder.Append($"{percentage * 100:F2}").Append("%{");
                    if (pair.Key.HasValue && pair.Key.Value == isLeft)
                        stringBuilder.Append("color:white;");
                    else
                        stringBuilder.Append("color:transparent;");
                    stringBuilder.Append('}');
                    currentTime += pair.Value;
                }
                stringBuilder.Append('}');
                styles[i] = stringBuilder.ToString();
            }
            left = styles[0];
            right = styles[1];
        }

    }
}
