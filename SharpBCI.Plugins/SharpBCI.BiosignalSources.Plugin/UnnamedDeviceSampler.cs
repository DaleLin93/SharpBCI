using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Threading;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Extensions;
using SharpBCI.Extensions.IO.Devices;
using SharpBCI.Extensions.IO.Devices.BiosignalSources;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.BiosignalSources
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public class UnnamedDeviceSampler : BiosignalSource
    {

        public const string DeviceName = "Unnamed Device (2kHz)";

        public class Factory : DeviceFactory<UnnamedDeviceSampler, IBiosignalSource>
        {

            public static readonly Parameter<string> SerialPortParam = Parameter<string>.CreateBuilder("Serial Port")
                .SetSelectableValues(SerialPort.GetPortNames)
                .SetMetadata(SelectablePresenter.RefreshableProperty, true)
                .Build();

            public Factory() : base(SerialPortParam) { }

            public override UnnamedDeviceSampler Create(IReadonlyContext context)
            {
                var serialPort = SerialPortParam.Get(context);
                if (serialPort == null) throw new UserException("Serial Port must set.");
                return new UnnamedDeviceSampler(serialPort);
            }

        }

        // ReSharper disable once NotAccessedField.Local

        private readonly SerialPort _serialPort;

        private IEnumerator<double[]> _enumerator;

        public UnnamedDeviceSampler(string portName, ushort channelNum = 8, double frequency = 2000) 
        {
            PortName = portName;
            ChannelNum = channelNum;
            Frequency = frequency;

            _serialPort = Open(portName);
        }

        private static SerialPort Open(string portName)
        {
            var serialPort = new SerialPort
            {
                PortName = portName,
                BaudRate = 921600,
                DataBits = 8,
                StopBits = StopBits.One,
                Parity = 0
            };
            try { serialPort.Open(); }
            catch (Exception e) { throw new IOException("Cannot open " + portName, e); }
            return serialPort;
        }

        private static List<double[]> ReadTwoSamples(SerialPort port, uint channelNum, long timeout)
        {
            var start = DateTimeUtils.CurrentTimeMillis;
            var startFlagBuf = new byte[2];
            var shortBuf = new byte[sizeof(short)];
            var result = new List<double[]>(2) {new double[channelNum], new double[channelNum]};
            var startBufFilled = 0;
            var startFlagReceived = false;
            var shortFilled = 0;
            var resultFilled = 0;
            do
            {
                if (port.BytesToRead == 0)
                {
                    Thread.Sleep(1);
                    continue;
                }
                if (!startFlagReceived)
                {
                    var b = port.ReadByte();
                    if (startBufFilled < 2)
                        startFlagBuf[startBufFilled++] = (byte)b;
                    else
                    {
                        startFlagBuf[0] = startFlagBuf[1];
                        startFlagBuf[1] = (byte)b;
                    }
                    if (startBufFilled == 2 && startFlagBuf[0] == 0x55 && startFlagBuf[1] == 0x56)
                        startFlagReceived = true;
                }
                else
                {
                    shortFilled += port.Read(shortBuf, shortFilled, sizeof(short) - shortFilled);
                    if (shortFilled == sizeof(short))
                    {
                        shortBuf.Reverse();
                        var val = BitConverter.ToInt16(shortBuf, 0) * 0.195D;
                        var sampleIndex = (int)(resultFilled / channelNum);
                        var channelIndex = (resultFilled + channelNum - 1) % channelNum;
                        result[sampleIndex][channelIndex] = val;
                        resultFilled++;
                        if (resultFilled == channelNum * 2)
                            return result;
                        shortFilled = 0;
                    }
                }
            } while (start + timeout > DateTimeUtils.CurrentTimeMillis);
            throw new TimeoutException();
        }

        public string PortName { get; }

        public override void Open() { }

        public override void Shutdown() => _serialPort.Close();

        public override ISample Read()
        {
            if (_enumerator == null || !_enumerator.MoveNext())
                (_enumerator = ReadTwoSamples(_serialPort, ChannelNum, 5000).GetEnumerator()).MoveNext();
            return new Sample(_enumerator.Current);
        }

        public override void Dispose() { }

    }
}
