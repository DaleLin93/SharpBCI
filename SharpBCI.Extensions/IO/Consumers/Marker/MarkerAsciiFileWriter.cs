using System.IO;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices.MarkerSources;

namespace SharpBCI.Extensions.IO.Consumers.Marker
{
    /// <summary>
    /// Marker ASCII file writer: *.mkr
    /// Format: 
    ///     Marker; Time (relative to session create time);
    /// </summary>
    [Consumer(ConsumerName, typeof(Factory), "1.0")]
    public class MarkerAsciiFileWriter : TimestampedFileWriter<IMarker>
    {

        public sealed class Factory : ConsumerFactory<Timestamped<IMarker>>
        {

            public override IConsumer<Timestamped<IMarker>> Create(Session session, IReadonlyContext context, byte? num) =>
                new MarkerAsciiFileWriter(session.GetDataFileName(FileSuffix, num), session.CreateTimestamp);

        }

        public const string FileSuffix = ".mkr";

        public const string ConsumerName = "Marker ASCII File Writer (*" + FileSuffix + ")";

        public MarkerAsciiFileWriter([NotNull] string fileName, long baseTime = 0, int bufferSize = 1024) : base(fileName, bufferSize, baseTime) { }

        protected override void Write(Stream stream, Timestamped<IMarker> data)
        {
            stream.WriteAscii(data.Value.Code);
            stream.WriteByte((byte)',');
            stream.WriteAscii(data.Timestamp - BaseTime);
            stream.WriteByte((byte)'\n');
        }

    }
}