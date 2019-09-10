using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MarukoLib.Lang;
using SharpBCI.Extensions.Presenters;
using Path = SharpBCI.Extensions.Data.Path;

namespace SharpBCI.Extensions.Devices
{

    public class GazeFileReader : EyeTracker
    {

        public const string DeviceName = "Gaze File Reader (*.gaze)";

        public class Factory : DeviceFactory<GazeFileReader>
        {

            public static readonly Parameter<Path> GazeFileParam = Parameter<Path>.CreateBuilder("Gaze File", new Path(""))
                .SetMetadata(PathPresenter.PathTypeProperty, PathPresenter.PathType.File)
                .SetMetadata(PathPresenter.FilterProperty, "Gaze File (*.gaze)|*.gaze")
                .SetMetadata(PathPresenter.CheckExistenceProperty, true)
                .Build();

            public Factory() : base(GazeFileReader.DeviceName, GazeFileParam) { }

            public override GazeFileReader Create(IReadonlyContext context)
            {
                var filePath = GazeFileParam.Get(context) ?? throw new ArgumentException("file must be specified");
                if (!filePath.Exists) throw new FileNotFoundException("gaze file not found", filePath.Value);
                return new GazeFileReader(filePath.Value);
            }

        }

        private readonly object _lock = new object();

        private readonly long _startTimestamp;

        private readonly Timestamped<double[]>[] _samples;

        private long _startTimeTicks;

        private int _sampleOffset;

        public GazeFileReader(string file, long? startTimestamp = null) : base(DeviceName)
        {
            File = file;

            var lines = System.IO.File.ReadAllLines(file);

            var samples = new LinkedList<Timestamped<double[]>>();
            foreach (var line in lines)
                samples.AddLast(ReadGazePoints(line));
            _samples = samples.ToArray();

            _startTimestamp = startTimestamp ?? (_samples.TryGet(0, out var gazePoint) ? gazePoint.TimeStamp : 0);
        }

        private static Timestamped<double[]> ReadGazePoints(string line) // IO Blocking
        {
            var strings = line.Split(',', ' ', ';');
            var doubles = new double[2];
            for (var i = 0; i < doubles.Length; i++)
                doubles[i] = double.Parse(strings[i]);
            return new Timestamped<double[]>(long.Parse(strings[strings.Length - 1]), doubles);
        }

        public string File { get; }

        public ulong LineReadCount => (ulong) _sampleOffset;

        public override void Open() => Reset();

        public override IGazePoint Read()
        {
            while (true)
                lock (_lock)
                {
                    if (_sampleOffset >= _samples.Length) return null;
                    var sampleValues = _samples[_sampleOffset++];
                    var gazePoint = new GazePoint(sampleValues.Value[0], sampleValues.Value[1]);
                    var waitUntil = _startTimeTicks + (sampleValues.TimeStamp - _startTimestamp) * TimeSpan.TicksPerMillisecond;
                    while (DateTimeUtils.CurrentTimeTicks < waitUntil) { }
                    return gazePoint;
                }
        }

        public override void Shutdown() { }

        public void Reset()
        {
            _startTimeTicks = DateTimeUtils.CurrentTimeTicks;
            _sampleOffset = 0;
        }

    }
}
