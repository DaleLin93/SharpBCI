using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Devices;
using SharpBCI.Extensions.Devices.EyeTrackers;

namespace SharpBCI.Extensions.Streamers
{

    /// <summary>
    /// Gaze point streamer.
    /// Streaming gaze point data from specific eye-tracker.
    /// </summary>
    public class GazePointStreamer : TimestampedStreamer<IGazePoint>
    {

        public sealed class Factory : IStreamerFactory
        {

            public Type ValueType => typeof(Timestamped<IGazePoint>);

            public IStreamer Create(IDevice device, IClock clock) => new GazePointStreamer((IEyeTracker)device, clock);

        }

        public readonly IEyeTracker EyeTracker;

        public GazePointStreamer(IEyeTracker eyeTracker, IClock clock) : base(nameof(GazePointStreamer), clock)
        {
            EyeTracker = eyeTracker;
            Started += (sender, e) => eyeTracker.Open();
            Stopped += (sender, e) => eyeTracker.Shutdown();
        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        public GazePointStreamer(IEyeTracker eyeTracker, IClock clock, IStreamConsumer<Timestamped<IGazePoint>> consumer) : this(eyeTracker, clock) => Attach(consumer);

        protected override Timestamped<IGazePoint> Acquire() => WithTimestamp(EyeTracker.Read() ?? throw new EndOfStreamException());

    }

    /// <summary>
    /// Gaze point ASCII file writer (<see cref="FileSuffix"/>).
    /// Format: 
    ///     X; Y; Time (relative to session create time);
    /// </summary>
    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class GazePointAsciiFileWriter : TimestampedFileWriter<IGazePoint>
    {

        public sealed class Factory : StreamConsumerFactory<Timestamped<IGazePoint>>
        {

            public override IStreamConsumer<Timestamped<IGazePoint>> Create(Session session, IReadonlyContext context, byte? num) => 
                new GazePointAsciiFileWriter(session.GetDataFileName(FileSuffix, num), session.CreateTimestamp);

        }

        public const string FileSuffix = ".gaz";

        public const string ConsumerName = "Gaze Point Ascii File Writer (*" + FileSuffix + ")";

        public GazePointAsciiFileWriter([NotNull] string fileName, long baseTime = 0, int bufferSize = 2048) : base(fileName, bufferSize, baseTime) { }

        protected override void Write(Stream stream, Timestamped<IGazePoint> data)
        {
            var point = data.Value;
            stream.WriteAscii(point.X);
            stream.WriteByte((byte)',');
            stream.WriteAscii(point.Y);
            stream.WriteByte((byte)',');
            stream.WriteAscii(data.Timestamp - BaseTime);
            stream.WriteByte((byte)'\n');
        }

    }

    /// <summary>
    /// Gaze point ASCII file writer (<see cref="FileSuffix"/>) in network order.
    /// Format:  (X (double) + Y (double) + time (long))
    /// Time is relative to session create time.
    /// </summary>
    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class GazePointBinaryFileWriter : TimestampedFileWriter<IGazePoint>
    {

        public sealed class Factory : StreamConsumerFactory<Timestamped<IGazePoint>>
        {

            public override IStreamConsumer<Timestamped<IGazePoint>> Create(Session session, IReadonlyContext context, byte? num) =>
                new GazePointBinaryFileWriter(session.GetDataFileName(FileSuffix, num), session.CreateTimestamp);

        }

        public const string FileSuffix = ".bgaz";

        public const string ConsumerName = "Gaze Point Binary File Writer (*" + FileSuffix + ")";

        private readonly byte[] _buf = new byte[Math.Max(sizeof(double), sizeof(long))];

        public GazePointBinaryFileWriter([NotNull] string fileName, long baseTime = 0, int bufferSize = 4096) : base(fileName, bufferSize, baseTime) { }

        protected override void Write(Stream stream, Timestamped<IGazePoint> sample)
        {
            lock (_buf)
            {
                var bytes = _buf;
                var gazePoint = sample.Value;
                bytes.WriteDoubleAsNetworkOrder(gazePoint.X);
                stream.Write(bytes, 0, sizeof(double));
                bytes.WriteDoubleAsNetworkOrder(gazePoint.Y);
                stream.Write(bytes, 0, sizeof(double));
                bytes.WriteInt64AsNetworkOrder(sample.Timestamp - BaseTime);
                stream.Write(bytes, 0, sizeof(long));
            }
        }

    }

}
