using System.IO;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices.EyeTrackers;

namespace SharpBCI.Extensions.IO.Consumers.GazePoint
{

    /// <summary>
    /// Gaze point ASCII file writer (<see cref="FileSuffix"/>).
    /// Format: 
    ///     X; Y; Time (relative to session create time);
    /// </summary>
    [Consumer(ConsumerName, typeof(Factory), "1.0")]
    public class GazePointAsciiFileWriter : TimestampedFileWriter<IGazePoint>
    {

        public sealed class Factory : ConsumerFactory<Timestamped<IGazePoint>>
        {

            public override IConsumer<Timestamped<IGazePoint>> Create(Session session, IReadonlyContext context, byte? num) =>
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
}
