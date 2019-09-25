using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Devices;

namespace SharpBCI.Extensions.Streamers
{

    /// <summary>
    /// Mark streamer.
    /// Streaming manually marked value.
    /// </summary>
    public class MarkStreamer : TimestampedStreamer<IMark>, IMarkable
    {

        public sealed class Factory : IStreamerFactory
        {

            public Type ValueType => typeof(Timestamped<IMark>);

            public IStreamer Create(IDevice device, IClock clock) => new MarkStreamer((IMarkSource)device, clock);

        }

        public readonly IMarkSource MarkSource;

        public MarkStreamer(IMarkSource markSource, IClock clock) : base(nameof(MarkStreamer), clock) => MarkSource = markSource;

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        public MarkStreamer(IMarkSource markSource, IClock clock, IStreamConsumer<Timestamped<IMark>> consumer) : this(markSource, clock) => Attach(consumer);

        public long Mark(string label, int marker)
        {
            var obj = WithTimestamp(new Mark(label, marker));
            Enqueue(obj);
            return obj.Timestamp;
        }

        protected override Timestamped<IMark> Acquire() => WithTimestamp(MarkSource.Read() ?? throw new EndOfStreamException());

    }

    /// <summary>
    /// Mark ASCII file writer: *.mrk
    /// Format: 
    ///     Marker; Time (relative to session create time);
    /// </summary>
    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class MarkAsciiFileWriter : TimestampedFileWriter<IMark>
    {

        public sealed class Factory : StreamConsumerFactory<Timestamped<IMark>>
        {

            public override IStreamConsumer<Timestamped<IMark>> Create(Session session, IReadonlyContext context, byte? num) =>
                new MarkAsciiFileWriter(session.GetDataFileName(FileSuffix, num), session.CreateTimestamp);

        }

        public const string FileSuffix = ".mrk";

        public const string ConsumerName = "Mark ASCII File Writer (*" + FileSuffix + ")";

        public MarkAsciiFileWriter([NotNull] string fileName, long baseTime = 0, int bufferSize = 1024) : base(fileName, bufferSize, baseTime) { }

        protected override void Write(Stream stream, Timestamped<IMark> data)
        {
            stream.WriteAscii(data.Value.Code);
            stream.WriteByte((byte)',');
            stream.WriteAscii(data.Timestamp - BaseTime);
            stream.WriteByte((byte)'\n');
        }

    }

}
