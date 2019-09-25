using MarukoLib.Lang;
using MarukoLib.Lang.Concurrent;
using SharpBCI.Extensions.Data;

namespace SharpBCI.Extensions.Devices
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public sealed class HeartbeatMarkSource : MarkSource
    {

        public const string DeviceName = "Heartbeat Mark Source";

        public static readonly Parameter<string> LabelParam = new Parameter<string>("Label", defaultValue: "Heartbeat");

        public static readonly Parameter<int> CodeParam = new Parameter<int>("Code", -1);

        public static readonly Parameter<TimeInterval> IntervalParam = new Parameter<TimeInterval>("Interval", new TimeInterval(1, TimeUnit.Second));

        public class Factory : DeviceFactory<HeartbeatMarkSource, IMarkSource>
        {

            public Factory() : base(LabelParam, CodeParam, IntervalParam) { }

            public override HeartbeatMarkSource Create(IReadonlyContext context) =>
                new HeartbeatMarkSource(LabelParam.Get(context), CodeParam.Get(context), IntervalParam.Get(context));

        }

        private readonly IMark _mark;

        private readonly FrequencyBarrier _frequencyBarrier;

        private volatile bool _started;

        private HeartbeatMarkSource(string label, int code, TimeInterval interval)
        {
            _mark = new Mark(label, code);
            _frequencyBarrier = new FrequencyBarrier.MinimumInterval(Clock.SystemMillisClock, interval.TimeSpan);
        }

        public override void Open() => _started = true;

        public override IMark Read()
        {
            while (!_frequencyBarrier.WaitOne(100))
                if (!_started)
                    return null;
            return _mark;
        }

        public override void Shutdown() => _started = false;

        public override void Dispose() { }

    }

}
