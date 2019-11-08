using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Extensions.Presenters;
using Path = SharpBCI.Extensions.Data.Path;

namespace SharpBCI.Extensions.IO.Devices.BiosignalSources
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public class DataFileReader : BiosignalSource
    {

        public const string DeviceName = "Data File Reader (*"+ BiosignalAsciiFileWriter.FileSuffix + ")";

        public class Factory : DeviceFactory<DataFileReader, IBiosignalSource>
        {

            public static readonly Parameter<Path> DataFileParam = Parameter<Path>.CreateBuilder("Data File", new Path(""))
                .SetMetadata(PathPresenter.PathTypeProperty, PathPresenter.PathType.File)
                .SetMetadata(PathPresenter.FilterProperty, FileUtils.GetFileFilter("Biosignal Data File", BiosignalAsciiFileWriter.FileSuffix))
                .SetMetadata(PathPresenter.CheckExistenceProperty, true)
                .Build();

            public static readonly Parameter<double> FrequencyParam = new Parameter<double>("Frequency", Predicates.Positive, 1);

            public Factory() : base(DataFileParam, FrequencyParam) { }

            public override DataFileReader Create(IReadonlyContext context)
            {
                var filePath = DataFileParam.Get(context) ?? throw new ArgumentException("file must be specified");
                if (!filePath.Exists) throw new FileNotFoundException("data file not found", filePath.Value);
                return new DataFileReader(filePath.Value, FrequencyParam.Get(context));
            }

        }

        private readonly object _lock = new object();

        private readonly long? _startTimestamp;

        private readonly Timestamped<double[]>[] _samples;

        private readonly long _sampleIntervalTicks;

        private long _startTimeTicks;

        private int _sampleOffset;

        public DataFileReader(string file, double frequency, long? startTimestamp = null) 
        {
            File = file;
            Frequency = frequency;
            _startTimestamp = startTimestamp;

            var lines = System.IO.File.ReadAllLines(file);
            var channelNum = ReadChannelValues(lines[0], null).Value.Length;
            ChannelNum = (ushort)channelNum;

            var samples = new LinkedList<Timestamped<double[]>>();
            foreach (var line in lines)
                samples.AddLast(ReadChannelValues(line, null));
            _samples = samples.ToArray();

            _sampleIntervalTicks = (long)Math.Ceiling(TimeSpan.TicksPerSecond / frequency);
        }

        private static Timestamped<double[]> ReadChannelValues(string line, IReadOnlyList<int> columnsSelector) // IO Blocking
        {
            var strings = line.Split(',', ' ');
            var doubles = new double[columnsSelector?.Count ?? strings.Length - 1];
            if (columnsSelector == null)
                for (var i = 0; i < doubles.Length; i++)
                    doubles[i] = double.Parse(strings[i]);
            else 
                for (var i = 0; i < columnsSelector.Count; i++)
                    doubles[i] = double.Parse(strings[columnsSelector[i]]);
            return new Timestamped<double[]>(long.Parse(strings[strings.Length - 1]), doubles);
        }

        public string File { get; }

        public ulong LineReadCount => (ulong) _sampleOffset;

        public override void Open() => Reset();

        public override ISample Read()
        {
            for (;;)
                lock (_lock)
                {
                    if (_sampleOffset >= _samples.Length) return null;
                    var sampleValues = _samples[_sampleOffset++];
                    var sample = new Sample(sampleValues.Value);
                    long waitUntil;
                    if (_startTimestamp == null)
                        waitUntil = _startTimeTicks + _sampleIntervalTicks * (long)LineReadCount;
                    else
                        waitUntil = _startTimeTicks + (sampleValues.Timestamp - _startTimestamp.Value) * TimeSpan.TicksPerMillisecond;
                    while (DateTimeUtils.CurrentTimeTicks < waitUntil) { }
                    return sample;
                }
        }

        public override void Shutdown() { }

        public void Reset()
        {
            _startTimeTicks = DateTimeUtils.CurrentTimeTicks;
            _sampleOffset = 0;
        }

        public override void Dispose() { }

    }
}
