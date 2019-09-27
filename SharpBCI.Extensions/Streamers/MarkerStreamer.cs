using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Devices;
using SharpBCI.Extensions.Devices.MarkerSources;

namespace SharpBCI.Extensions.Streamers
{

    /// <summary>
    /// Mark streamer.
    /// Streaming manually marked value.
    /// </summary>
    public class MarkerStreamer : TimestampedStreamer<IMarker>, IMarkable
    {

        public sealed class Factory : IStreamerFactory
        {

            public Type ValueType => typeof(Timestamped<IMarker>);

            public IStreamer Create(IDevice device, IClock clock) => new MarkerStreamer((IMarkerSource)device, clock);

        }

        public readonly IMarkerSource MarkerSource;

        public MarkerStreamer(IMarkerSource markerSource, IClock clock) : base(nameof(MarkerStreamer), clock)
        {
            MarkerSource = markerSource;
            Started += (sender, e) => MarkerSource.Open();
            Stopped += (sender, e) => MarkerSource.Shutdown();
        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        public MarkerStreamer(IMarkerSource markerSource, IClock clock, IStreamConsumer<Timestamped<IMarker>> consumer) : this(markerSource, clock) => Attach(consumer);

        public long Mark(string label, int marker)
        {
            var obj = WithTimestamp(new Marker(label, marker));
            Enqueue(obj);
            return obj.Timestamp;
        }

        protected override Timestamped<IMarker> Acquire() => WithTimestamp(MarkerSource.Read() ?? throw new EndOfStreamException());

    }

    /// <summary>
    /// Marker ASCII file writer: *.mkr
    /// Format: 
    ///     Marker; Time (relative to session create time);
    /// </summary>
    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class MarkerAsciiFileWriter : TimestampedFileWriter<IMarker>
    {

        public sealed class Factory : StreamConsumerFactory<Timestamped<IMarker>>
        {

            public override IStreamConsumer<Timestamped<IMarker>> Create(Session session, IReadonlyContext context, byte? num) =>
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
