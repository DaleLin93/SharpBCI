using System;
using System.Threading;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Extensions;
using SharpBCI.Extensions.IO.Devices;
using SharpBCI.Extensions.IO.Devices.EyeTrackers;
using Tobii.Interaction;
using Tobii.Interaction.Client;

namespace SharpBCI.EyeTrackers
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public class TobiiEyeTracker : EyeTracker
    {

        public const string DeviceName = "Tobii's Eye-Tracker";

        public class Factory : DeviceFactory<TobiiEyeTracker, IEyeTracker>
        {

            public static readonly Parameter<string> HostNameParam = new Parameter<string>("Host Name", defaultValue: "SharpBCI");

            public Factory() : base(HostNameParam) { }

            public override TobiiEyeTracker Create(IReadonlyContext context)
            {
                switch (Host.EyeXAvailability)
                {
                    case EyeXAvailability.NotAvailable:
                        throw new StateException("Tobii software is unavailable");
                    case EyeXAvailability.NotRunning:
                        throw new StateException("Tobii software is not running");
                    default:
                        return new TobiiEyeTracker(HostNameParam.Get(context));
                }
            }

        }

        private readonly object _lock = new object();

        private readonly AutoResetEvent _signal = new AutoResetEvent(false);

        private readonly Host _host;

        private readonly bool _autoDispose;

        private readonly GazePointDataStream _gazePointDataStream;

        private volatile bool _stopped = true;

        private IGazePoint _lastGazePoint;

        public TobiiEyeTracker([NotNull] string hostName) : this(new Host(hostName ?? throw new ArgumentNullException(nameof(hostName)))) { }

        public TobiiEyeTracker([NotNull] Host host, bool autoDispose = true)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _autoDispose = autoDispose;
            _gazePointDataStream = host.Streams.CreateGazePointDataStream();
            _gazePointDataStream.IsEnabled = false;
        }

        ~TobiiEyeTracker()
        {
            if (_autoDispose) _host.Dispose();
        }

        public override void Open()
        {
            _signal.Reset();
            _stopped = false;
            _gazePointDataStream.Next += GazePointDataStream_Next;
            _gazePointDataStream.IsEnabled = true;
        }

        public override void Shutdown()
        {
            _gazePointDataStream.IsEnabled = false;
            _gazePointDataStream.Next -= GazePointDataStream_Next;
            _stopped = true;
            _lastGazePoint = null;
            _signal.Set();
        }

        public override IGazePoint Read()
        {
            while (!_stopped)
            {
                lock (_lock)
                {
                    if (!_signal.WaitOne(500)) continue;
                    return _lastGazePoint;
                }
            }
            return null;
        }

        public override void Dispose() { }

        private void GazePointDataStream_Next(object sender, StreamData<GazePointData> data)
        {
            var gazePoint = data.Data;
            _lastGazePoint = new GazePoint(gazePoint.X, gazePoint.Y);
            _signal.Set();
        }

    }

}
