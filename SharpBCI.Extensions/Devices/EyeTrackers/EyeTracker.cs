using MarukoLib.Lang;
using SharpBCI.Extensions.Streamers;

namespace SharpBCI.Extensions.Devices.EyeTrackers
{

    public interface IGazePoint
    {

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

        public GazePoint(double px, double py)
        {
            X = px;
            Y = py;
        }

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
            new GazePointVisualizationWindow(new GazePointStreamer(eyeTracker, Clock.SystemMillisClock), 50).Show();
        }

    }

}
