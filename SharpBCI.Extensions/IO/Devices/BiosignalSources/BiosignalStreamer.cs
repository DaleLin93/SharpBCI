using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using MarukoLib.Lang;
using SharpBCI.Core.IO;

namespace SharpBCI.Extensions.IO.Devices.BiosignalSources
{

    /// <summary>
    /// Bio-signal streamer.
    /// Streaming bio-signal data from specific bio-signal sampler.
    /// </summary>
    public class BiosignalStreamer : TimestampedStreamer<ISample>
    {

        public sealed class Factory : IStreamerFactory
        {

            public Type StreamingType => typeof(Timestamped<ISample>);

            public IStreamer Create(IDevice device, IClock clock) => device == null ? null : new BiosignalStreamer((IBiosignalSource)device, clock);

        }

        public readonly IBiosignalSource BiosignalSource;

        private readonly uint[] _channelIndices;

        public BiosignalStreamer(IBiosignalSource biosignalSource, IClock clock, ArrayQuery channelSelector = null) : base(nameof(BiosignalStreamer), clock) 
        {
            BiosignalSource = biosignalSource;
            Started += (sender, e) => biosignalSource.Open();
            Stopped += (sender, e) => biosignalSource.Shutdown();
            _channelIndices = channelSelector?.Enumerate(1, biosignalSource.ChannelNum).Select(val => (uint) (val - 1)).ToArray();
        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        public BiosignalStreamer(IBiosignalSource biosignalSource, IClock clock, IConsumer<Timestamped<ISample>> consumer, ArrayQuery channelSelector = null) 
            : this(biosignalSource, clock, channelSelector) => AttachConsumer(consumer);

        public uint[] SelectedChannelIndices => (uint[]) _channelIndices?.Clone();

        protected override Timestamped<ISample> Acquire()
        {
            var sample = BiosignalSource.Read() ?? throw new EndOfStreamException();
            if (_channelIndices != null) sample = sample.Select(_channelIndices);
            return WithTimestamp(sample);
        } 

    }
}
