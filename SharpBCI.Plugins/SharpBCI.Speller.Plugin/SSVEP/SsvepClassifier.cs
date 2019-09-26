using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Devices.BiosignalSources;

namespace SharpBCI.Paradigms.Speller.SSVEP
{

    public interface ISsvepClassifier : IStreamConsumer<Timestamped<ISample>>
    {

        bool Actived { get; set; }

        int Classify();

    }

    internal abstract class AbstractSsvepClassifier : StreamConsumer<Timestamped<ISample>>, ISsvepClassifier
    {

        protected readonly IClock Clock;

        protected readonly uint[] ChannelIndices;

        protected readonly double SamplingRate;

        protected readonly uint TrialDurationMs;

        protected readonly uint SsvepDelayMs;

        private readonly LinkedList<double[]> _samples = new LinkedList<double[]>();

        private bool _actived = false;

        private int _discardCount;

        protected AbstractSsvepClassifier([NotNull] IClock clock, [NotNull] uint[] channelIndices, double samplingRate, uint trialDurationMs, uint ssvepDelayMs)
        {
            Clock = clock;
            ChannelIndices = (uint[])channelIndices.Clone();
            SamplingRate = samplingRate;
            TrialDurationMs = trialDurationMs;
            SsvepDelayMs = ssvepDelayMs;
            WindowSize = (uint)(samplingRate * trialDurationMs / 1000.0);
        }

        public uint WindowSize { get; }

        public bool Actived
        {
            get => _actived;
            set
            {
                if (value == _actived)
                    return;
                if (value)
                {
                    Interlocked.Exchange(ref _discardCount, (int)(SsvepDelayMs / 1000.0 * SamplingRate));
                    lock (_samples)
                        _samples.Clear();
                }
                _actived = value;
            }
        }

        public override void Accept(Timestamped<ISample> data)
        {
            lock (_samples)
                if (_samples.Count < WindowSize && Interlocked.Decrement(ref _discardCount) <= 0)
                    _samples.AddLast(data.Value[ChannelIndices]);
        }

        public abstract int Classify();

        protected bool TryGetSamples(out IEnumerable<double[]> samples)
        {
            var startTime = Clock.Time;
            while (true)
            {
                lock (_samples)
                    if (_samples.Count >= WindowSize)
                    {
                        samples = _samples.ToArray();
                        break;
                    }

                if (Clock.Unit.ConvertTo(Clock.Time - startTime, TimeUnit.Millisecond) > TrialDurationMs)
                {
                    samples = default;
                    return false;
                }
            }
            return true;
        }

    }
    
}
