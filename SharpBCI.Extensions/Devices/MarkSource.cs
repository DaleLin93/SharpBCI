using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions.Streamers;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Devices
{

    public interface IMark
    {

        [CanBeNull] string Label { get; }

        int Code { get; }

    }

    public struct Mark : IMark
    {

        public Mark([CanBeNull] string label, int code)
        {
            Label = label;
            Code = code;
        }

        public string Label { get; }

        public int Code { get; }

    }

    [DeviceType("Mark Source", IsRequired = true,
        StreamerFactory = typeof(MarkStreamer.Factory),
        DataVisualizer = typeof(MarkSourceDataVisualizer))]
    public interface IMarkSource : IDevice
    {

        IMark Read();

    }

    public abstract class MarkSource : Device, IMarkSource
    {

        public abstract IMark Read();

    }

    internal class MarkSourceDataVisualizer : IDataVisualizer
    {

        public void Visualize(IDevice device)
        {
            var markSource = (IMarkSource)device;
            new MarkDisplayWindow(new MarkStreamer(markSource, Clock.SystemMillisClock), 10).Show();
        }

    }

}
