using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;

namespace SharpBCI.Extensions.Devices.MarkerSources
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public sealed class SerialPortMarkerSource : MarkerSource
    {

        public const string DeviceName = "Serial Port Marker Source";

        public static readonly Parameter<string> SerialPortParam = Parameter<string>.CreateBuilder("Serial Port")
            .SetSelectableValues(SerialPort.GetPortNames)
            .Build();

        public static readonly Parameter<int> BaudRateParam = new Parameter<int>("Baud Rate", Predicates.Positive, 115200);

        public static readonly Parameter<byte> DataBitsParam = new Parameter<byte>("Data Bits", 8);

        public static readonly Parameter<StopBits> StopBitsParam = Parameter<StopBits>.OfEnum("Stop Bits", StopBits.One);

        public static readonly Parameter<Parity> ParityParam = Parameter<Parity>.OfEnum("Parity", 0);

        public class Factory : DeviceFactory<SerialPortMarkerSource, IMarkerSource>
        {

            public Factory() : base(SerialPortParam, BaudRateParam, DataBitsParam, StopBitsParam, ParityParam) { }

            public override SerialPortMarkerSource Create(IReadonlyContext context)
            {
                var serialPort = SerialPortParam.Get(context);
                if (serialPort == null) throw new UserException("Serial Port must set.");
                return new SerialPortMarkerSource(serialPort, BaudRateParam.Get(context), DataBitsParam.Get(context), StopBitsParam.Get(context), ParityParam.Get(context));
            }
        }

        private readonly SerialPort _serialPort;

        private readonly LinkedList<IMarker> _marks = new LinkedList<IMarker>();

        private readonly Semaphore _semaphore = new Semaphore(0, int.MaxValue);

        private bool _started = false;

        private SerialPortMarkerSource(string serialPortName, int baudRate, byte dataBits, StopBits stopBits, Parity parity)
        {
            _serialPort = new SerialPort(serialPortName) {BaudRate = baudRate, DataBits = dataBits, StopBits = stopBits, Parity = parity};
            _serialPort.Open();
        }

        public override void Open()
        {
            _started = true;
            _serialPort.DataReceived += SerialPortOnDataReceived;
        }

        public override IMarker Read()
        {
            while (!_semaphore.WaitOne(100))
                if (!_started) return null;
            lock (_marks)
            {
                var mark = _marks.First.Value;
                _marks.RemoveFirst();
                return mark;
            }
        }

        public override void Shutdown()
        {
            _serialPort.DataReceived -= SerialPortOnDataReceived;
            _started = false;
            lock (_marks)
            {
                while (_marks.Count > 0)
                {
                    _marks.RemoveLast();
                    _semaphore.WaitOne(0);
                }
            }
        }

        public override void Dispose() => _serialPort.Dispose();

        private void SerialPortOnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (e.EventType == SerialData.Eof)
            {
                Shutdown();
                return;
            }

            lock (_serialPort)
            {
                while (_serialPort.BytesToRead > 0)
                {
                    var b = _serialPort.ReadByte();
                    var mark = new Marker(new string((char) b, 1), b);
                    lock (_marks) _marks.AddLast(mark);
                    _semaphore.Release();
                }
            }
        }

    }

}
