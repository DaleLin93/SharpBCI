using MarukoLib.Lang;

namespace SharpBCI.Extensions.IO.Devices.BiosignalSources
{

    public interface ISample
    {

        double[] Values { get; }

        double[] this[params int[] indices] { get; }

        double[] this[params uint[] indices] { get; }

        ISample Select(params uint[] indices);

    }

    public class Sample : ISample
    {

        public Sample(double[] values) => Values = values;

        public double[] Values { get; }

        public double[] this[params int[] indices]
        {
            get
            {
                var original = Values;
                var mapped = new double[indices.Length];
                for (var i = 0; i < indices.Length; i++)
                    mapped[i] = original[indices[i]];
                return mapped;
            }
        }

        public double[] this[params uint[] indices]
        {
            get
            {
                var original = Values;
                var mapped = new double[indices.Length];
                for (var i = 0; i < indices.Length; i++)
                    mapped[i] = original[indices[i]];
                return mapped;
            }
        }

        public Sample Select(params uint[] indices) => new Sample(this[indices]);

        public override string ToString() => $"{nameof(Values)}: {Values.Join(", ")}";

        ISample ISample.Select(params uint[] indices) => Select(indices);

    }

    [DeviceType("Biosignal Source", 
        StreamerFactory = typeof(BiosignalStreamer.Factory),
        DataVisualizer = typeof(BiosignalSourceDataVisualizer))]
    public interface IBiosignalSource : IDevice
    {

        ushort ChannelNum { get; }

        double Frequency { get; }

        ISample Read();

    }

    public abstract class BiosignalSource : Device, IBiosignalSource
    {

        public ushort ChannelNum { get; protected set; }

        public double Frequency { get; protected set; }

        public abstract ISample Read();

    }

    internal class BiosignalSourceDataVisualizer : IDataVisualizer
    {

        public void Visualize(IDevice device)
        {
            var biosignalSource = (IBiosignalSource) device;
            new BiosignalVisualizationWindow(new BiosignalStreamer(biosignalSource, Clock.SystemMillisClock), 
                biosignalSource.ChannelNum, (long) (biosignalSource.Frequency * 5)).ShowAndRunRenderLoop();
        }

    }

}
