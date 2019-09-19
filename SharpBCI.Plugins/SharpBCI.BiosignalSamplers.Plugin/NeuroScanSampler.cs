using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices;

namespace SharpBCI.BiosignalSamplers
{

    [SuppressMessage("ReSharper", "UnusedMember.Local")]
    public class NeuroScanSampler : BiosignalSampler
    {

        public const string DeviceName = "NeuroScan";

        public class Factory : DeviceFactory<NeuroScanSampler, IBiosignalSampler>
        {

            public static readonly Parameter<string> IpAddressParam = new Parameter<string>("IP Address", defaultValue: "127.0.0.1");

            public static readonly Parameter<int> PortParam = new Parameter<int>("Port", 9889);

            public Factory() : base(NeuroScanSampler.DeviceName, IpAddressParam, PortParam) { }

            public override NeuroScanSampler Create(IReadonlyContext context) =>
                new NeuroScanSampler(IPAddress.Parse(IpAddressParam.Get(context)), PortParam.Get(context));

        }

        public enum PacketType
        {
            Ctrl, File, Data
        }

        public enum DataType : short
        {
            InfoBlock = 1,
            EegData = 2
        }

        public enum ControlCode : ushort
        {
            General = 1,
            Server = 2,
            Client = 3
        }

        private class PacketHeader
        {

            public const int HeaderSize = 12;

            public readonly char[] Type;
            public readonly ushort Code; // Control code, 1 (General), 2 (Server), or 3 (Client)
            public readonly ushort Request; // see the NeuroScan Acquire manual
            public readonly uint BodySize;

            public PacketHeader(PacketType packetType, ControlCode code, ushort req, uint bodySize)
            {
                Type = packetType.GetPacketType();
                Code = (ushort) code;
                Request = req;
                BodySize = bodySize;
            }

            public PacketHeader(byte[] data, int offset)
            {
                Type = Encoding.ASCII.GetChars(data, offset, 4); // 4
                Code = data.ReadUInt16FromNetworkOrder(offset + 4); // 2
                Request = data.ReadUInt16FromNetworkOrder(offset + 6); // 2
                BodySize = data.ReadUInt32FromNetworkOrder(offset + 8); // 4
            }

            public bool TryParsedPacketType(out PacketType packetType)
            {
                packetType = default;
                foreach (var pt in Enum.GetValues(typeof(PacketType)).OfType<PacketType>())
                    if (pt.IsTypeMatch(Type))
                    {
                        packetType = pt;
                        return true;
                    }
                return false;
            }

            public byte[] ToByteArray()
            {
                var bytes = new byte[HeaderSize];
                WriteBytes(bytes, 0);
                return bytes;
            }

            // ReSharper disable once UnusedMethodReturnValue.Local
            public int WriteBytes(byte[] array, int offset)
            {
                offset = array.WriteValues(Encoding.ASCII.GetBytes(Type), offset);
                offset = array.WriteUInt16AsNetworkOrder(Code, offset);
                offset = array.WriteUInt16AsNetworkOrder(Request, offset);
                offset = array.WriteUInt32AsNetworkOrder(BodySize, offset);
                return offset;
            }

            public override string ToString()
            {
                return $"{nameof(Code)}: {Code}\n" +
                       $"{nameof(Request)}: {Request}\n" +
                       $"{nameof(BodySize)}: {BodySize}\n" +
                       $"{nameof(Type)}: {new string(Type)}\n";
            }
        }

        private class SettingsPacket
        {

            public const Endianness ByteOrder = Endianness.LittleEndian;

            public const int PacketSize = 28;

            public readonly int Size, ChannelNum, EventChannelNum, SamplesPerBlock, SamplingRate, DataSize;

            public readonly float Resolution; // the value in micro-volts

            public SettingsPacket(byte[] buf, int offset)
            {
                Size = buf.ReadInt32(ByteOrder, offset);
                ChannelNum = buf.ReadInt32(ByteOrder, offset + 4);
                EventChannelNum = buf.ReadInt32(ByteOrder, offset + 8);
                SamplesPerBlock = buf.ReadInt32(ByteOrder, offset + 12);
                SamplingRate = buf.ReadInt32(ByteOrder, offset + 16);
                DataSize = buf.ReadInt32(ByteOrder, offset + 20);
                Resolution = buf.ReadSingle(ByteOrder, offset + 24);
            }

            public int BodySize => SamplesPerBlock * (ChannelNum + EventChannelNum) * DataSize;

            public bool IsValid => SamplesPerBlock != 0 && SamplingRate != 0 && DataSize != 0 && ChannelNum != 0;

            public override string ToString()
            {
                return $"{nameof(Size)}: {Size}, " +
                       $"{nameof(ChannelNum)}: {ChannelNum}, " +
                       $"{nameof(EventChannelNum)}: {EventChannelNum}, " +
                       $"{nameof(SamplesPerBlock)}: {SamplesPerBlock}, " +
                       $"{nameof(SamplingRate)}: {SamplingRate}, " +
                       $"{nameof(DataSize)}: {DataSize}, " +
                       $"{nameof(Resolution)}: {Resolution}";
            }
        }

        public const ushort GeneralControlCode = 1;
        public const ushort ClosingUp = 2;

        public const ushort ServerControlCode = 2;
        public const ushort StartAcquisition = 1;
        public const ushort StopAcquisition = 2;

        public const ushort ClientControlCode = 3;
        public const ushort RequestStartData = 3;
        public const ushort RequestStopData = 4;
        public const ushort RequestBasicInfo = 5;

        public const ushort InfoTypeBasicInfo = 3;

        public const ushort DataTypeRaw16Bits = 1;
        public const ushort DataTypeRaw32Bits = 2;

        private readonly object _lock = new object();

        private readonly TcpClient _tcpClient;

        private readonly Stream _stream;

        private readonly SettingsPacket _settings;

        private readonly byte[] _dataBuffer;

        private IEnumerator<ISample> _enumerator;

        public NeuroScanSampler(int port) : this(IPAddress.Loopback, port) { }

        public NeuroScanSampler(IPAddress address, int port) : this(new IPEndPoint(address, port)) { }

        public NeuroScanSampler(IPEndPoint endPoint) : base(DeviceName)
        {
            EndPoint = endPoint;

            _tcpClient = new TcpClient();
            _tcpClient.Connect(endPoint);
            _tcpClient.ReceiveTimeout = 100;
            _stream = _tcpClient.GetStream();
            _settings = Handshake(_stream, 1000);
            _dataBuffer = new byte[Math.Max(_settings.BodySize, PacketHeader.HeaderSize)];

             ChannelNum = (ushort)_settings.ChannelNum;
             Frequency = _settings.SamplingRate;
    }

        private static void ReadStreamFully(Stream stream, byte[] buf, int offset, int size, long timeoutMillis) => 
            stream.ReadFully(buf, offset, size, timeoutMillis, ex => ex.ErrorCode == 10060);

        private static void SendCommand(Stream stream, ControlCode ctrlCode, ushort reqNum) => stream.WriteFully(new PacketHeader(PacketType.Ctrl, ctrlCode, reqNum, 0).ToByteArray());

        private static PacketHeader ReadMessageHeader(Stream stream, long timeout) => ReadMessageHeader(stream, new byte[PacketHeader.HeaderSize], 0, timeout);

        private static PacketHeader ReadMessageHeader(Stream stream, byte[] buf, int offset, long timeout)
        {
            ReadStreamFully(stream, buf, offset, PacketHeader.HeaderSize, timeout);
            return new PacketHeader(buf, offset);
        }

        private static SettingsPacket Handshake(Stream stream, long timeout)
        {
            SendCommand(stream, ControlCode.Client, RequestBasicInfo);
            var headerBuffer = new byte[Math.Max(PacketHeader.HeaderSize, SettingsPacket.PacketSize)];
            var header = ReadMessageHeader(stream, headerBuffer, 0, timeout);

            ReadStreamFully(stream, headerBuffer, 0, SettingsPacket.PacketSize, timeout);
            if (header.Code == (short)DataType.InfoBlock 
                && header.Request == InfoTypeBasicInfo
                && header.BodySize == SettingsPacket.PacketSize)
                return new SettingsPacket(headerBuffer, 0);
            throw new IOException("通信失败");
        }

        private static IEnumerable<ISample> ReadBlock(Stream stream, SettingsPacket settings, byte[] buffer, long timeout)
        {
            while (true)
            {
                var header = ReadMessageHeader(stream, buffer, 0, timeout);
                if (!PacketType.Data.IsTypeMatch(header.Type))
                {
                    stream.SkipBytes(header.BodySize);
                    continue;
                }
                ReadStreamFully(stream, buffer, 0, (int)header.BodySize, timeout);
                break;
            }

            var samples = new List<ISample>(settings.SamplesPerBlock);
            var offset = 0;
            for (var i = 0; i < settings.SamplesPerBlock; i++)
            {
                var channels = new double[settings.ChannelNum];
                for (var j = 0; j < settings.ChannelNum; j++)
                {
                    double rawValue;
                    switch (settings.DataSize)
                    {
                        case 2:
                            rawValue = buffer.ReadInt16(Endianness.LittleEndian, offset);
                            break;
                        case 4:
                            rawValue = buffer.ReadInt32(Endianness.LittleEndian, offset);
                            break;
                        default:
                            rawValue = 0;
                            break;
                    }
                    channels[j] = rawValue * settings.Resolution / 1000000;
                    offset += settings.DataSize;
                }
                // var markers = new int[settings.EventChannelNum];
                for (var j = 0; j < settings.EventChannelNum; j++)
                    offset += settings.DataSize; // Ignore markers
                samples.Add(new GenericSample(channels));
            }
            return samples;
        }

        public IPEndPoint EndPoint { get; }

        public override void Open()
        {
            SendCommand(_stream, ControlCode.Server, StartAcquisition);
            SendCommand(_stream, ControlCode.Client, RequestStartData);
        }

        public override ISample Read()
        {
            lock (_lock)
                while (true)
                {
                    if (_enumerator?.MoveNext() ?? false)
                        return _enumerator.Current;
                    _enumerator = ReadBlock(_stream, _settings, _dataBuffer, 8000).GetEnumerator();
                }
        }

        public override void Shutdown()
        {
            SendCommand(_stream, ControlCode.Client, RequestStopData);
            SendCommand(_stream, ControlCode.Server, StopAcquisition);
            SendCommand(_stream, ControlCode.General, ClosingUp);
            _stream.Flush();
            try { Thread.Sleep(1200); } catch (Exception) { /* ignored */ }
            _tcpClient.Close();
            _tcpClient.Dispose();
            try { Thread.Sleep(500); } catch (Exception) { /* ignored */ }
        }

    }

    internal static class PacketTypeExtensions
    {
        private static readonly char[] CtrlPacketType = "CTRL".ToCharArray();
        private static readonly char[] FilePacketType = "FILE".ToCharArray();
        private static readonly char[] DataPacketType = "DATA".ToCharArray();

        public static char[] GetPacketType(this NeuroScanSampler.PacketType type) => (char[])GetPacketRaw(type).Clone();

        public static bool IsTypeMatch(this NeuroScanSampler.PacketType type, char[] chars) => GetPacketRaw(type).SequenceEqual(chars);

        private static char[] GetPacketRaw(NeuroScanSampler.PacketType type)
        {
            switch (type)
            {
                case NeuroScanSampler.PacketType.Ctrl:
                    return CtrlPacketType;
                case NeuroScanSampler.PacketType.File:
                    return FilePacketType;
                case NeuroScanSampler.PacketType.Data:
                    return DataPacketType;
                default:
                    throw new NotSupportedException();
            }
        }

    }

}
