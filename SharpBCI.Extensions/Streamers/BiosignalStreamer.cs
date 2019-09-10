using System;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Devices;

namespace SharpBCI.Extensions.Streamers
{

    /// <summary>
    /// Bio-signal streamer.
    /// Streaming bio-signal data from specific bio-signal sampler.
    /// </summary>
    public class BiosignalStreamer : TimestampedStreamer<ISample>
    {

        public sealed class Factory : IStreamerFactory
        {

            public Type ValueType => typeof(Timestamped<ISample>);

            public IStreamer Create(IDevice device, IClock clock) => new BiosignalStreamer((IBiosignalSampler)device, clock);

        }

        public readonly IBiosignalSampler BiosignalSampler;

        private readonly uint[] _channelIndices;

        public BiosignalStreamer(IBiosignalSampler biosignalSampler, IClock clock, ArrayQuery channelSelector = null) : base(nameof(BiosignalStreamer), clock) 
        {
            BiosignalSampler = biosignalSampler;
            Started += (sender, e) => biosignalSampler.Open();
            Stopped += (sender, e) => biosignalSampler.Shutdown();
            _channelIndices = channelSelector?.Enumerate(1, biosignalSampler.ChannelNum).Select(val => (uint) (val - 1)).ToArray();
        }

        public BiosignalStreamer(IBiosignalSampler biosignalSampler, IClock clock, IConsumer<Timestamped<ISample>> consumer, ArrayQuery channelSelector = null) 
            : this(biosignalSampler, clock, channelSelector) => Attach(consumer);

        public uint[] SelectedChannelIndices => (uint[]) _channelIndices?.Clone();

        protected override Timestamped<ISample> Acquire()
        {
            var sample = BiosignalSampler.Read() ?? throw new EndOfStreamException();
            if (_channelIndices != null) sample = sample.Select(_channelIndices);
            return WithTimestamp(sample);
        } 

    }

    /// <summary>
    /// Bio-signal data file writer: *.dat
    /// Format:  (N channels + 1 time)
    ///     C1, C2, C3, ..., Cn, Time (relative to session create time);
    /// </summary>
    public class BiosignalDataFileWriter : TimestampedFileWriter<ISample>
    {

        public sealed class Factory : ConsumerFactory<Timestamped<ISample>>
        {

            public Factory() : base($"Biosignal Data File Writer (*{FileSuffix})") { }

            public override IConsumer<Timestamped<ISample>> Create(Session session, IReadonlyContext context, byte? num) =>
                new BiosignalDataFileWriter(session.GetDataFileName(FileSuffix, num), session.CreateTimestamp);

        }

        public const string FileSuffix = ".dat";

        public BiosignalDataFileWriter([NotNull] string fileName, long baseTime = 0, int bufferSize = 4096) : base(fileName, bufferSize, baseTime) { }

        protected override void Write(Stream stream, Timestamped<ISample> sample)
        {
            foreach (var value in sample.Value.Values)
            {
                stream.WriteAscii(value);
                stream.WriteByte((byte)',');
            }
            stream.WriteAscii(sample.TimeStamp - BaseTime);
            stream.WriteByte((byte)'\n');
        }

    }

}
