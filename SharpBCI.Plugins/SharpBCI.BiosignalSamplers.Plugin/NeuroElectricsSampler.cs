using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices;

namespace SharpBCI.BiosignalSamplers
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public class NeuroElectricsSampler : BiosignalSampler
    {

        public const string DeviceName = "NeuroElectrics";

        public class Factory : DeviceFactory<NeuroElectricsSampler, IBiosignalSampler>
        {

            public static readonly Parameter<string> IpAddressParam = new Parameter<string>("IP Address", defaultValue: "127.0.0.1");

            public static readonly Parameter<int> PortParam = new Parameter<int>("Port", 1234);

            public Factory() : base(IpAddressParam, PortParam) { }

            public override NeuroElectricsSampler Create(IReadonlyContext context) => 
                new NeuroElectricsSampler(IPAddress.Parse(IpAddressParam.Get(context)), PortParam.Get(context));

        }

        private const int NumOfChannel = 8;

        private readonly TcpClient _tcpClient;

        private readonly Stream _stream;

        private readonly ThreadLocal<byte[]> _localBuf;

        public NeuroElectricsSampler(int port) : this(IPAddress.Loopback, port) { }

        public NeuroElectricsSampler(IPAddress address, int port) : this(new IPEndPoint(address, port)) { }

        public NeuroElectricsSampler(IPEndPoint endPoint)
        {
            _localBuf = new ThreadLocal<byte[]>(() => new byte[4 * NumOfChannel]);
            _tcpClient = new TcpClient();
            _tcpClient.Connect(endPoint);
            _tcpClient.ReceiveTimeout = 100;
            _stream = _tcpClient.GetStream();

            ChannelNum = NumOfChannel;
            Frequency = 500;
        }

        private static double[] ReadValues(Stream stream, byte[] buf, long timeout)
        {
            stream.ReadFully(buf, timeout);
            var values = new int[NumOfChannel];
            for (var i = 0; i < NumOfChannel; i++)
            {
                var readOffset = i * sizeof(int);
                if (BitConverter.IsLittleEndian)
                {
                    buf.Swap(readOffset + 0, readOffset + 3);
                    buf.Swap(readOffset + 1, readOffset + 2);
                }
                values[i] = BitConverter.ToInt32(buf, readOffset);
            }
            throw new TimeoutException();
        }

        public override void Open() { }

        public override void Shutdown() => _tcpClient.Close();

        public override ISample Read() => new GenericSample(ReadValues(_stream, _localBuf.Value, 2000));

        public override void Dispose() { }

    }
}
