using MarukoLib.Lang;
using SharpBCI.Extensions.Streamers;

namespace SharpBCI.Extensions.Devices
{

    public interface ISample
    {

        double[] Values { get; }

        double[] this[params int[] indices] { get; }

        double[] this[params uint[] indices] { get; }

        ISample Select(params uint[] indices);

    }

    public class GenericSample : ISample
    {

        public GenericSample(double[] values) => Values = values;

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

        public GenericSample Select(params uint[] indices) => new GenericSample(this[indices]);

        public override string ToString() => $"{nameof(Values)}: {Values.Join(", ")}";

        ISample ISample.Select(params uint[] indices) => Select(indices);

    }

    [DeviceType("biosignalSampler", "Biosignal Sampler", StreamerFactoryType = typeof(BiosignalStreamer.Factory))]
    public interface IBiosignalSampler : IDevice
    {

        ushort ChannelNum { get; }

        double Frequency { get; }

        ISample Read();

    }

    public abstract class BiosignalSampler : Device, IBiosignalSampler
    {

        public ushort ChannelNum { get; protected set; }

        public double Frequency { get; protected set; }

        public abstract ISample Read();

    }

}
