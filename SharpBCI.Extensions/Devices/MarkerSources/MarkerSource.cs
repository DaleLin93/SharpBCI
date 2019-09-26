using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions.Streamers;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Devices.MarkerSources
{

    public interface IMarker
    {

        [CanBeNull] string Label { get; }

        int Code { get; }

    }

    public struct Marker : IMarker
    {

        public Marker([CanBeNull] string label, int code)
        {
            Label = label;
            Code = code;
        }

        public string Label { get; }

        public int Code { get; }

    }

    [DeviceType("Marker Source", IsRequired = true,
        StreamerFactory = typeof(MarkerStreamer.Factory),
        DataVisualizer = typeof(MarkerSourceDataVisualizer))]
    public interface IMarkerSource : IDevice
    {

        IMarker Read();

    }

    public abstract class MarkerSource : Device, IMarkerSource
    {

        public abstract IMarker Read();

    }

    internal class MarkerSourceDataVisualizer : IDataVisualizer
    {

        public void Visualize(IDevice device)
        {
            var markSource = (IMarkerSource)device;
            new MarkerDisplayWindow(new MarkerStreamer(markSource, Clock.SystemMillisClock), 10).Show();
        }

    }

}
