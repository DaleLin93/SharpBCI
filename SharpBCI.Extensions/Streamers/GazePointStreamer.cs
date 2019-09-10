using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
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

        public GazePointStreamer(IEyeTracker eyeTracker, IClock clock, IConsumer<Timestamped<IGazePoint>> consumer) : this(eyeTracker, clock) => Attach(consumer);

        protected override Timestamped<IGazePoint> Acquire() => WithTimestamp(EyeTracker.Read() ?? throw new EndOfStreamException());

    }

    /// <summary>
    /// Gaze point file writer: *.gaze
    /// Format: 
    ///     X; Y; Time (relative to session create time);
    /// </summary>
    public class GazePointFileWriter : TimestampedFileWriter<IGazePoint>
    {

        public sealed class Factory : ConsumerFactory<Timestamped<IGazePoint>>
        {

            public Factory() : base($"Gaze Point File Writer (*{FileSuffix})") { }

            public override IConsumer<Timestamped<IGazePoint>> Create(Session session, IReadonlyContext context, byte? num) => 
                new GazePointFileWriter(session.GetDataFileName(FileSuffix), session.CreateTimestamp);

        }

        public const string FileSuffix = ".gaze";

        public GazePointFileWriter([NotNull] string fileName, long baseTime = 0, int bufferSize = 2048) : base(fileName, bufferSize, baseTime) { }

        protected override void Write(Stream stream, Timestamped<IGazePoint> data)
        {
            var point = data.Value;
            stream.WriteAscii(point.X);
            stream.WriteByte((byte)';');
            stream.WriteAscii(point.Y);
            stream.WriteByte((byte)';');
            stream.WriteAscii(data.TimeStamp - BaseTime);
            stream.WriteByte((byte)'\n');
        }

    }

}
