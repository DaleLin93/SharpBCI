using System;
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
