using System.Threading;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices;
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
                        return new TobiiEyeTracker(new Host(HostNameParam.Get(context)));
                }
            }

        }

        private readonly object _lock = new object();

        private readonly AutoResetEvent _signal = new AutoResetEvent(false);

        private readonly GazePointDataStream _gazePointDataStream;

        private volatile bool _stopped = true;

        private IGazePoint _lastGazePoint;

        public TobiiEyeTracker(Host host)
        {
            _gazePointDataStream = host.Streams.CreateGazePointDataStream();
            _gazePointDataStream.IsEnabled = false;
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
