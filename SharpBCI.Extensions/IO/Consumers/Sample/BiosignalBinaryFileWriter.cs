using System;
using System.IO;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices.BiosignalSources;

namespace SharpBCI.Extensions.IO.Consumers.Sample
{
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
                bytes.WriteInt64AsNetworkOrder(sample.Timestamp - BaseTime);
                stream.Write(bytes, 0, sizeof(long));
            }
        }

    }
}