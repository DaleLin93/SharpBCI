using MarukoLib.Lang;
using MarukoLib.Lang.Concurrent;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.Extensions.IO.Devices.MarkerSources
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public sealed class HeartbeatGenerator : MarkerSource
    {

        public const string DeviceName = "Heartbeat Generator";

        public static readonly Parameter<string> LabelParam = new Parameter<string>("Label", defaultValue: "Heartbeat");

        public static readonly Parameter<int> CodeParam = Parameter<int>.CreateBuilder("Code", MarkerDefinitions.HeartbeatMarker)
            .SetMetadata(Presenters.Presenters.PresenterProperty, MarkerDefinitionPresenter.Instance)
            .SetMetadata(MarkerDefinitionPresenter.CustomizeMarkerCodeProperty, true)
            .Build();

        public static readonly Parameter<TimeInterval> IntervalParam = new Parameter<TimeInterval>("Interval", new TimeInterval(1, TimeUnit.Second));

        public class Factory : DeviceFactory<HeartbeatGenerator, IMarkerSource>
        {

            public Factory() : base(LabelParam, CodeParam, IntervalParam) { }

            public override HeartbeatGenerator Create(IReadonlyContext context) =>
                new HeartbeatGenerator(LabelParam.Get(context), CodeParam.Get(context), IntervalParam.Get(context));

        }

        private readonly IMarker _marker;

        private readonly FrequencyBarrier _frequencyBarrier;

        private volatile bool _started;

        private HeartbeatGenerator(string label, int code, TimeInterval interval)
        {
            _marker = new Marker(label, code);
            _frequencyBarrier = new FrequencyBarrier.MinimumInterval(Clock.SystemMillisClock, interval.TimeSpan);
        }

        public override void Open() => _started = true;

        public override IMarker Read()
        {
            while (!_frequencyBarrier.WaitOne(100))
                if (!_started)
                    return null;
            return _marker;
        }

        public override void Shutdown() => _started = false;

        public override void Dispose() { }

    }

}
