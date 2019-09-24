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
    /// Gaze point file writer: *.gaze
    /// Format: 
    ///     X; Y; Time (relative to session create time);
    /// </summary>
    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class GazePointFileWriter : TimestampedFileWriter<IGazePoint>
    {

        public sealed class Factory : StreamConsumerFactory<Timestamped<IGazePoint>>
        {

            public override IStreamConsumer<Timestamped<IGazePoint>> Create(Session session, IReadonlyContext context, byte? num) => 
                new GazePointFileWriter(session.GetDataFileName(FileSuffix), session.CreateTimestamp);

        }

        public const string FileSuffix = ".gaz";

        public const string ConsumerName = "Gaze Point File Writer (*" + FileSuffix + ")";

        public GazePointFileWriter([NotNull] string fileName, long baseTime = 0, int bufferSize = 2048) : base(fileName, bufferSize, baseTime) { }

        protected override void Write(Stream stream, Timestamped<IGazePoint> data)
        {
            var point = data.Value;
            stream.WriteAscii(point.X);
            stream.WriteByte((byte)',');
            stream.WriteAscii(point.Y);
            stream.WriteByte((byte)',');
            stream.WriteAscii(data.TimeStamp - BaseTime);
            stream.WriteByte((byte)'\n');
        }

    }

}
