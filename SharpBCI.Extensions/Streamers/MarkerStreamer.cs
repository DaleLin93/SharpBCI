using System.Diagnostics.CodeAnalysis;
using System.IO;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.IO;

namespace SharpBCI.Extensions.Streamers
{

    /// <summary>
    /// Marker streamer.
    /// Streaming manually marked value.
    /// </summary>
    public class MarkerStreamer : TimestampedStreamer<int>, IMarkable
    {

        public MarkerStreamer(IClock clock) : base(nameof(MarkerStreamer), clock) { }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        public MarkerStreamer(IClock clock, IStreamConsumer<Timestamped<int>> consumer) : this(clock) => Attach(consumer);

        public long Mark(int marker)
        {
            var obj = WithTimestamp(marker);
            Enqueue(obj);
            return obj.TimeStamp;
        }

        protected override Timestamped<int> Acquire() => throw new EndOfStreamException();

    }

    /// <summary>
    /// Gaze point file writer: *.mrk
    /// Format: 
    ///     Marker; Time (relative to session create time);
    /// </summary>
    public class MarkerFileWriter : TimestampedFileWriter<int>
    {

        public const string FileSuffix = ".mrk";

        public MarkerFileWriter([NotNull] string fileName, long baseTime = 0, int bufferSize = 1024) : base(fileName, bufferSize, baseTime) { }

        protected override void Write(Stream stream, Timestamped<int> data)
        {
            stream.WriteAscii(data.Value);
            stream.WriteByte((byte)',');
            stream.WriteAscii(data.TimeStamp - BaseTime);
            stream.WriteByte((byte)'\n');
        }

    }

}
