using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Diagnostics.CodeAnalysis;
using MarukoLib.Lang;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices;

namespace SharpBCI.BiosignalSamplers
{

    /**
     *
     * OpenBCI V3 Protocol.
     *
     * SAMPLE_START_BYTE
     * 
     * SAMPLE_INDEX
     *
     * EEG Samples, values are 24-bit signed, MSB first.
     * Bytes 3-5: Data value for EEG channel 1
     * Bytes 6-8: Data value for EEG channel 2
     * Bytes 9-11: Data value for EEG channel 3
     * Bytes 12-14: Data value for EEG channel 4
     * Bytes 15-17: Data value for EEG channel 5
     * Bytes 18-20: Data value for EEG channel 6
     * Bytes 21-23: Data value for EEG channel 7
     * Bytes 24-26: Data value for EEG channel 8
     *
     * Accelerometer data, values are 16-bit signed, MSB first
     * Bytes 27-28: Data value for accelerometer channel X
     * Bytes 29-30: Data value for accelerometer channel Y
     * Bytes 31-32: Data value for accelerometer channel Z
     *
     * SAMPLE_STOP_BYTE
     *
     * Commands:
     * 1. v - version
     * 2. b - begin
     * 3. s - stop
     *
     */
    [SuppressMessage("ReSharper", "InconsistentNaming")]
    [Device(DeviceName, typeof(Factory), "1.0")]
    public class OpenBCISampler : BiosignalSampler
    {

        public const string DeviceName = "OpenBCI";

        public class Factory : DeviceFactory<OpenBCISampler, IBiosignalSampler>
        {

            public static readonly Parameter<string> SerialPortParam = new Parameter<string>("Serial Port", defaultValue: null);

            public static readonly Parameter<bool> DaisyModuleParam = new Parameter<bool>("Daisy Module", false);

            public Factory() : base(SerialPortParam, DaisyModuleParam) { }

            public override OpenBCISampler Create(IReadonlyContext context) => new OpenBCISampler(SerialPortParam.Get(context), DaisyModuleParam.Get(context));

        }

        private const byte SampleStartByte = 0xA0;

        private const byte SampleStopByte = 0xC0;

        private readonly ThreadLocal<byte[]> _localBuf;

        private readonly SerialPort _serialPort;

        public OpenBCISampler(bool daisyMode = false) : this(null, daisyMode) { }

        public OpenBCISampler(string serialPortName, bool daisyMode = false) : this(serialPortName, (ushort) (daisyMode ? +16 : +8), daisyMode ? 250 : 500) { }

        public OpenBCISampler(string serialPortName, ushort channelNum, double frequency) 
        {
            SerialPortName = serialPortName;
            ChannelNum = channelNum;
            Frequency = frequency;

            _localBuf = new ThreadLocal<byte[]>(() => new byte[1 + 3 * channelNum + 2 * 3 + 1]);
            _serialPort = Open(serialPortName);
        }

        private static double[] ReadValues(SerialPort port, byte[] buf, uint channelNum, long timeout)  {
            var intBuf = new byte[4];
            var start = DateTimeUtils.CurrentTimeMillis;
            var startByteReceived = false;
            var offset = 0;
            do {
                if (port.BytesToRead == 0) {
                    Thread.Sleep(1);
                    continue;
                }
                if (!startByteReceived) {
                    if (port.ReadByte() == SampleStartByte)
                        startByteReceived = true;
                } else {
                    offset += port.Read(buf, offset, buf.Length - offset);
                    if (offset < buf.Length) continue;
                    // Check end byte
                    if (buf[buf.Length - 1] == SampleStopByte) // Complete data
                    {
                        // Decode data, start from 1, ignore first byte(index).
                        var values = new double[channelNum];
                        for (var i = 0; i < channelNum; i++)
                        {
                            var startIndex = 1 + i * 3;
                            if (BitConverter.IsLittleEndian)
                            {
                                intBuf[3] = buf[startIndex + 0];
                                intBuf[2] = buf[startIndex + 1];
                                intBuf[1] = buf[startIndex + 2];
                            }
                            else
                            {
                                intBuf[0] = buf[startIndex + 0];
                                intBuf[1] = buf[startIndex + 1];
                                intBuf[2] = buf[startIndex + 2];
                            }
                            values[i] = (BitConverter.ToInt32(intBuf, 0) >> 8) / 100.0;
                        }
                        return values;
                    }

                    // find position of next start byte, and shift data.
                    startByteReceived = false; // reset state.
                    var startBytePos = -1;
                    for (var i = 0; i < buf.Length; i++)
                    {
                        if (buf[i] != SampleStartByte) continue;
                        startBytePos = i;
                        break;
                    }
                    if (startBytePos == -1)
                        offset = 0;
                    else
                    {
                        startByteReceived = true;
                        var copyFrom = startBytePos + 1;
                        if (copyFrom < buf.Length)
                            Array.Copy(buf, copyFrom, buf, 0, buf.Length - copyFrom);
                    }
                }
            } while (start + timeout > DateTimeUtils.CurrentTimeMillis);
            throw new TimeoutException();
        }
        
        private static string ReadString(SerialPort port,  long timeout) {
            var stringBuilder = new StringBuilder(64);
            var count = 0;
            var start = DateTimeUtils.CurrentTimeMillis;
            do
            {
                if (port.BytesToRead <= 0)
                    continue;
                var b = (char) port.ReadByte();
                if (b == '$')
                {
                    count++;
                    if (count >= 3)
                        return stringBuilder.ToString();
                }
                else
                {
                    if (count > 0)
                    {
                        for (var i = 0; i < count; i++)
                            stringBuilder.Append('$');
                        count = 0;
                    }
                    stringBuilder.Append(b); // convert to ASCII
                }
            } while (start + timeout > DateTimeUtils.CurrentTimeMillis);
            throw new TimeoutException();
        }

        private static void Validate(SerialPort port)
        {
            try
            {
                port.Write("s"); // Stop.
                Thread.Sleep(100);
                while (port.BytesToRead > 0)  // Consume all buffered data.
                    port.ReadByte();
                port.Write("v"); // Version command.
                var response = ReadString(port, 5000);
                if (!response.StartsWith("OpenBCI"))
                    throw new IOException("Unexpected response: " + response);
            }
            catch (IOException) { throw; }
            catch (Exception e) { throw new IOException("Port validation failed", e); }
        }
        
        private static SerialPort Open(string portName)
        {
            IEnumerable<string> portNames = portName == null 
                ? SerialPort.GetPortNames() : new[] { portName };

            var serialPort = new SerialPort {BaudRate = 115200, DataBits = 8, StopBits = StopBits.One, Parity = 0};
            foreach (var name in portNames)
            {
                serialPort.PortName = name;
                try
                {
                    serialPort.Open();
                    Validate(serialPort);
                    return serialPort;
                }
                catch (Exception e)
                {
                    if (serialPort.IsOpen) serialPort.Close();
                    if (Equals(portName, name)) throw new IOException("Cannot open " + name + ": " + e.Message);
                }
            }
            throw new IOException("Failed to open port");
        }

        public string SerialPortName { get; }

        public override void Open() => _serialPort.Write("b");

        public override ISample Read() => new GenericSample(ReadValues(_serialPort, _localBuf.Value, ChannelNum, 2000));

        public override void Shutdown()
        {
            _serialPort.Write("s");
            _serialPort.Close();
        }

    }
}
