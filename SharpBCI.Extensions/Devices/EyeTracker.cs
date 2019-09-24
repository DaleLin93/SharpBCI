using MarukoLib.Lang;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Streamers;

namespace SharpBCI.Extensions.Devices
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

    [DeviceType("eyeTracker", "Eye-Tracker", StreamerFactoryType = typeof(GazePointStreamer.Factory))]
    public interface IEyeTracker : IDevice
    {

        IGazePoint Read();

    }

    public abstract class EyeTracker : Device, IEyeTracker
    {

        public abstract IGazePoint Read();

    }

}
