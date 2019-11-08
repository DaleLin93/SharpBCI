using System;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.IO.Devices.BiosignalSources
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public class GenericOscillator : BiosignalSource
    {

        public const string DeviceName = "Generic Oscillator";

        public class Factory : DeviceFactory<GenericOscillator, IBiosignalSource>
        {

            public static readonly Parameter<ushort> ChannelNumParam = new Parameter<ushort>("Channel Num", 8);

            public static readonly Parameter<double> SamplingRateParam = new Parameter<double>("Sampling Rate", 500);

            public static readonly Parameter<double> WaveFrequencyParam = new Parameter<double>("Wave Frequency", 15);

            public Factory() : base(ChannelNumParam, SamplingRateParam, WaveFrequencyParam) { }

            public override GenericOscillator Create(IReadonlyContext context) => new GenericOscillator(ChannelNumParam.Get(context), SamplingRateParam.Get(context), WaveFrequencyParam.Get(context));

        }

        private readonly double[] _channelAngles;

        private readonly double _step;

        private readonly long _sampleIntervalTicks;

        private long _lastTimeTicks = 0;

        public GenericOscillator(ushort channelNum, double frequency, double waveFrequency) 
        {
            ChannelNum = channelNum;
            Frequency = frequency;

            _channelAngles = new double[channelNum];
            _step = 360 / (frequency * (1 / waveFrequency));
            for (var i = 0; i < channelNum; i++)
                _channelAngles[i] = i * (360.0 / channelNum);

            _sampleIntervalTicks = (long)Math.Ceiling(TimeSpan.TicksPerSecond / frequency);
        }

        public override void Open() => _lastTimeTicks = DateTimeUtils.CurrentTimeTicks;

        public override ISample Read()
        {
            long currentTicks;
            long passedTicks;
            while ((passedTicks = ((currentTicks = DateTimeUtils.CurrentTimeTicks) - _lastTimeTicks)) < _sampleIntervalTicks) { }
            _lastTimeTicks = currentTicks;
            var stepCount = passedTicks / _sampleIntervalTicks;
            var samples = new double[ChannelNum];
            for (var i = 0; i < ChannelNum; i++)
            {
                var angle = (_channelAngles[i] += stepCount * _step);
                samples[i] = Math.Sin(angle / 180 * Math.PI);
            }
            return new Sample(samples);
        }

        public override void Shutdown() { }

        public override void Dispose() { }

    }
}
