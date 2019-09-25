﻿using System;
using System.Diagnostics.CodeAnalysis;
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

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        public BiosignalStreamer(IBiosignalSampler biosignalSampler, IClock clock, IStreamConsumer<Timestamped<ISample>> consumer, ArrayQuery channelSelector = null) 
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
    /// Bio-signal ASCII data file writer (<see cref="FileSuffix"/>).
    /// Format:  (N channels + 1 time)
    ///     C1, C2, C3, ..., Cn, Time (relative to session create time);
    /// </summary>
    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class BiosignalAsciiFileWriter : TimestampedFileWriter<ISample>
    {

        public sealed class Factory : StreamConsumerFactory<Timestamped<ISample>>
        {

            public override IStreamConsumer<Timestamped<ISample>> Create(Session session, IReadonlyContext context, byte? num) =>
                new BiosignalAsciiFileWriter(session.GetDataFileName(FileSuffix, num), session.CreateTimestamp);

        }

        public const string FileSuffix = ".dat";

        public const string ConsumerName = "Biosignal ACII File Writer (*" + FileSuffix + ")";

        public BiosignalAsciiFileWriter([NotNull] string fileName, long baseTime = 0, int bufferSize = 4096) : base(fileName, bufferSize, baseTime) { }

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

    /// <summary>
    /// Bio-signal binary data file writer (<see cref="FileSuffix"/>) in network order.
    /// Format:  (N channels (doubles) + 1 time (long))
    /// Time is relative to session create time.
    /// </summary>
    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class BiosignalBinaryFileWriter : TimestampedFileWriter<ISample>
    {

        public sealed class Factory : StreamConsumerFactory<Timestamped<ISample>>
        {

            public override IStreamConsumer<Timestamped<ISample>> Create(Session session, IReadonlyContext context, byte? num) =>
                new BiosignalBinaryFileWriter(session.GetDataFileName(FileSuffix, num), session.CreateTimestamp);

        }

        public const string FileSuffix = ".bin";

        public const string ConsumerName = "Biosignal Binary File Writer (*" + FileSuffix + ")";
        
        private readonly byte[] _buf = new byte[Math.Max(sizeof(double), sizeof(long))];

        public BiosignalBinaryFileWriter([NotNull] string fileName, long baseTime = 0, int bufferSize = 4096) : base(fileName, bufferSize, baseTime) { }

        protected override void Write(Stream stream, Timestamped<ISample> sample)
        {
            lock (_buf)
            {
                var bytes = _buf;
                foreach (var value in sample.Value.Values)
                {
                    bytes.WriteDoubleAsNetworkOrder(value);
                    stream.Write(bytes, 0, sizeof(double));
                }
                bytes.WriteInt64AsNetworkOrder(sample.TimeStamp - BaseTime);
                stream.Write(bytes, 0, sizeof(long));
            }
        }

    }

}
