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
            stream.WriteAscii(sample.Timestamp - BaseTime);
            stream.WriteByte((byte)'\n');
        }

    }
}