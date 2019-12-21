using System.Collections.Generic;
using System.Linq;
using System.Threading;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices.BiosignalSources;

namespace SharpBCI.Paradigms.Speller.SSVEP
{

    public interface ISsvepIdentifier : IConsumer<Timestamped<ISample>>
    {

        bool IsActive { get; set; }

        double WindowSizeInSecs { get; }

        uint HarmonicsCount { get; }

        IdentificationResult Identify();

    }

    public abstract class AbstractSsvepIdentifier : Core.IO.Consumer<Timestamped<ISample>>, ISsvepIdentifier
    {

        protected readonly IClock Clock;

        protected readonly uint[] ChannelIndices;

        protected readonly uint TrialDurationMs;

        protected readonly uint SsvepDelayMs;

        private readonly LinkedList<double[]> _samples = new LinkedList<double[]>();

        private bool _active;

        private int _discardCount;

        protected AbstractSsvepIdentifier([NotNull] IClock clock, [NotNull] uint[] channelIndices, double samplingRate, uint trialDurationMs, uint ssvepDelayMs, uint harmonicsCount)
        {
            Clock = clock;
            ChannelIndices = (uint[])channelIndices.Clone();
            SamplingRate = samplingRate;
            TrialDurationMs = trialDurationMs;
            SsvepDelayMs = ssvepDelayMs;
            WindowSizeInSamples = (uint)(samplingRate * trialDurationMs / 1000.0);
            HarmonicsCount = harmonicsCount;
        }

        public double SamplingRate { get; }

        public uint WindowSizeInSamples { get; }

        public double WindowSizeInSecs => WindowSizeInSamples / SamplingRate;

        public uint HarmonicsCount { get; }

        public bool IsActive
        {
            get => _active;
            set
            {
                if (value == _active)
                    return;
                if (value)
                {
                    Interlocked.Exchange(ref _discardCount, (int)(SsvepDelayMs / 1000.0 * SamplingRate));
                    lock (_samples)
                        _samples.Clear();
                }
                _active = value;
            }
        }

        public override void Accept(Timestamped<ISample> data)
        {
            lock (_samples)
                if (_samples.Count < WindowSizeInSamples && Interlocked.Decrement(ref _discardCount) <= 0)
                    _samples.AddLast(data.Value[ChannelIndices]);
        }

        public abstract IdentificationResult Identify();

        protected bool TryGetSamples(out IEnumerable<double[]> samples)
        {
            var startTime = Clock.Time;
            for (;;)
            {
                lock (_samples)
                    if (_samples.Count >= WindowSizeInSamples)
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
