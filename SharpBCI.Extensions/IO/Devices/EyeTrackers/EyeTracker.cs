using MarukoLib.Lang;

namespace SharpBCI.Extensions.IO.Devices.EyeTrackers
{

    public interface IGazePoint
    {

        bool IsOutOfScreen { get; }

        /// <summary>
        /// Unit: px
        /// </summary>
        double X { get; }

        /// <summary>
        /// Unit: px
        /// </summary>
        double Y { get; }

    }

    public struct GazePoint : IGazePoint
    {

        public static readonly GazePoint OutOfScreen = new GazePoint(double.NaN, double.NaN);

        public GazePoint(double px, double py)
        {
            IsOutOfScreen = double.IsNaN(px) || double.IsNaN(py);
            X = px;
            Y = py;
        }

        public bool IsOutOfScreen { get; }

        public double X { get; }

        public double Y { get; }

    }

    [DeviceType("Eye-Tracker", 
        StreamerFactory = typeof(GazePointStreamer.Factory),
        DataVisualizer = typeof(EyeTrackerDataVisualizer))]
    public interface IEyeTracker : IDevice
    {

        IGazePoint Read();

    }

    public abstract class EyeTracker : Device, IEyeTracker
    {

        public abstract IGazePoint Read();

    }

    internal class EyeTrackerDataVisualizer : IDataVisualizer
    {

        public void Visualize(IDevice device)
        {
            var eyeTracker = (IEyeTracker)device;
            new GazePointVisualizationWindow(new GazePointStreamer(eyeTracker, Clock.SystemMillisClock), 50).ShowAndRunRenderLoop();
        }

    }

}
