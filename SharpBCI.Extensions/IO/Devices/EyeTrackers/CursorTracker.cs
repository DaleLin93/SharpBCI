﻿using System;
using System.Windows.Forms;
using MarukoLib.Lang;
using MarukoLib.Lang.Concurrent;

namespace SharpBCI.Extensions.IO.Devices.EyeTrackers
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public class CursorTracker : EyeTracker
    {

        public const string DeviceName = "Cursor Tracker";

        public class Factory : DeviceFactory<CursorTracker, IEyeTracker>
        {

            public static readonly Parameter<double> MaxFrequencyParam = new Parameter<double>("Max Frequency", 100);

            public Factory() : base(MaxFrequencyParam) { }

            public override CursorTracker Create(IReadonlyContext context) => new CursorTracker(MaxFrequencyParam.Get(context));

        }

        private readonly object _lock = new object();

        private readonly FrequencyBarrier _frequencyBarrier;

        public CursorTracker(double maxFrequency) 
        {
            MaxFrequency = maxFrequency;
            _frequencyBarrier = new FrequencyBarrier.MinimumInterval(Clock.SystemMillisClock, TimeSpan.FromSeconds(1 / maxFrequency));
        }

        public double MaxFrequency { get; }

        public override void Open() { }

        public override IGazePoint Read()
        {
            for (;;)
                lock (_lock)
                {
                    _frequencyBarrier.WaitOne();
                    var position = Cursor.Position;
                    return new GazePoint(position.X, position.Y);
                }
        }

        public override void Shutdown() { }

        public override void Dispose() { }

    }

}
