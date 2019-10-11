using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices.MarkerSources;
using SharpBCI.Extensions.Streamers;

namespace SharpBCI.EGI
{

    /// <summary>
    /// See: https://github.com/Psychtoolbox-3/Psychtoolbox-3/blob/fab0b49fd38ec477e3b4573f23dbd7766b0a89aa/Psychtoolbox/PsychHardware/NetStation.m
    /// </summary>
    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class NetStationTagging : StreamConsumer<Timestamped<IMarker>>, IDisposable
    {

        public const string ConsumerName = "Net Station Tagging";

        public class Factory : StreamConsumerFactory<Timestamped<IMarker>>
        {

            public static readonly Parameter<string> IpAddressParam = new Parameter<string>("IP Address", defaultValue: "127.0.0.1");

            public static readonly Parameter<int> PortParam = new Parameter<int>("Port", 55513);

            public static readonly Parameter<ushort> SyncLimitParam = new Parameter<ushort>("Sync Limit", "ms", null, 5);

            public static readonly Parameter<ushort> SyncRetryCountParam = new Parameter<ushort>("Sync Retry Count", 1000);

            public Factory() : base(IpAddressParam, PortParam, SyncLimitParam, SyncRetryCountParam) { }

            public override IStreamConsumer<Timestamped<IMarker>> Create(Session session, IReadonlyContext context, byte? num) =>
                new NetStationTagging(IPAddress.Parse(IpAddressParam.Get(context)), PortParam.Get(context),
                    TimeSpan.FromMilliseconds(SyncLimitParam.Get(context)), SyncRetryCountParam.Get(context));

        }

        private readonly TcpClient _tcpClient;

        private readonly Stream _stream;

        private long _syncBaseTime;
        
        public NetStationTagging(int port, TimeSpan syncLimit, ushort syncRetryCount) : this(IPAddress.Loopback, port, syncLimit, syncRetryCount) { }

        public NetStationTagging(IPAddress address, int port, TimeSpan syncLimit, ushort syncRetryCount) : this(new IPEndPoint(address, port), syncLimit, syncRetryCount) { }

        public NetStationTagging(IPEndPoint endPoint, TimeSpan syncLimit, ushort syncRetryCount)
        {
            EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
            SyncLimit = syncLimit;
            SyncRetryCount = syncRetryCount;
            _tcpClient = Connect(endPoint);
            _stream = _tcpClient.GetStream();
            Start();
        }

        ~NetStationTagging() => Stop();

        public IPEndPoint EndPoint { get; }

        public TimeSpan SyncLimit { get; }

        public ushort SyncRetryCount { get; }

        private static TcpClient Connect(IPEndPoint endPoint)
        {
            TcpClient tcpClient = null;
            try
            {
                tcpClient = new TcpClient();
                tcpClient.Connect(endPoint);
                tcpClient.ReceiveTimeout = 100;
                var stream = tcpClient.GetStream();

                stream.WriteFully(new[] { (byte)'Q', (byte)'M', (byte)'A', (byte)'C', (byte)'-' });
                stream.Flush();
                switch ((char)stream.ReadByte())
                {
                    case 'F':
                        throw new IOException("Connection: ECI error");
                    case 'I':
                        if (stream.ReadByte() != 1) throw new IOException("Connection: Unknown ECI version");
                        break;
                }
            }
            catch (Exception)
            {
                tcpClient?.Close();
                throw;
            }
            return tcpClient;
        }

        [SuppressMessage("ReSharper", "StringLiteralTypo")]
        private static void GetKeyCodeData(object keyCode, out string type, out byte[] data)
        {
            type = "NULL";
            data = EmptyArray<byte>.Instance;
            if (keyCode == null) return;

            switch (keyCode)
            {
                case bool b:
                    type = "bool";
                    data = new[] { b ? (byte)1 : (byte)0 };
                    break;
                case char c:
                    type = "byte";
                    data = new[] { (byte)c };
                    break;
                case byte b:
                    type = "shor";
                    data = new byte[sizeof(short)];
                    data.WriteInt16AsNetworkOrder(b);
                    break;
                case sbyte sb:
                    type = "shor";
                    data = new byte[sizeof(short)];
                    data.WriteInt16AsNetworkOrder(sb);
                    break;
                case short s:
                    type = "shor";
                    data = new byte[sizeof(short)];
                    data.WriteInt16AsNetworkOrder(s);
                    break;
                case ushort us:
                    type = "inte";
                    data = new byte[sizeof(int)];
                    data.WriteInt32AsNetworkOrder(us);
                    break;
                case int i:
                    type = "inte";
                    data = new byte[sizeof(int)];
                    data.WriteInt32AsNetworkOrder(i);
                    break;
                case float f:
                    type = "floa";
                    data = new byte[sizeof(float)];
                    data.WriteSingleAsNetworkOrder(f);
                    break;
                case uint ui:
                    type = "long";
                    data = new byte[sizeof(long)];
                    data.WriteInt64AsNetworkOrder(ui);
                    break;
                case long l:
                    type = "long";
                    data = new byte[sizeof(long)];
                    data.WriteInt64AsNetworkOrder(l);
                    break;
                case ulong ul:
                    type = "doub";
                    data = new byte[sizeof(double)];
                    data.WriteDoubleAsNetworkOrder(ul);
                    break;
                case double d:
                    type = "doub";
                    data = new byte[sizeof(double)];
                    data.WriteDoubleAsNetworkOrder(d);
                    break;
                case string s:
                    type = "utf8";
                    data = Encoding.UTF8.GetBytes(s);
                    break;
            }
        }

        public long Synchronize()
        {
            var start = DateTimeUtils.CurrentTimeMillis;
            var retry = 0;
            long diff;
            do
            {
                var bytes = new byte[sizeof(int)];
                _stream.WriteByte((byte) 'A');
                _stream.Flush();
                _stream.ReadByte();
                var syncTime = DateTimeUtils.CurrentTimeMillis - start;
                _stream.WriteByte((byte) 'T');
                bytes.WriteInt32AsNetworkOrder((int) syncTime);
                _stream.Write(bytes, 0, sizeof(int));
                _stream.Flush();
                _stream.ReadByte();
                var ackTime = DateTimeUtils.CurrentTimeMillis - start;
                diff = (ackTime - syncTime) / 2;
                if (diff <= SyncLimit.TotalMilliseconds) return start;
                retry++;
            } while (retry <= SyncRetryCount);
            throw new IOException($"Synchronization did not succeed within {SyncLimit.TotalMilliseconds:N1} ms\n Synchronization accuracy is {diff} ms");
        }

        public void SendEvent(int code, bool waitForAck = false) => SendEvent(code.ToString().TrimOrPadLeft(4, '0'), code, waitForAck);

        public void SendEvent(string name, int code, bool waitForAck = false) => SendEvent(name, new[] {new KeyValuePair<string, object>("CODE", code)}, waitForAck);

        public void SendEvent(string name, KeyValuePair<string, object>[] keys, bool waitForAck = false) => SendEvent(name, 1, keys, waitForAck);

        public void SendEvent(string name, uint duration, KeyValuePair<string, object>[] keys, bool waitForAck = false) =>
            SendEvent(name, (int) (DateTimeUtils.CurrentTimeMillis - _syncBaseTime), duration, keys, waitForAck);

        public void SendEvent(string name, int startTime, uint duration, KeyValuePair<string, object>[] keys, bool waitForAck = false)
        {
            var keyLength = 15;
            var keyDataList = new LinkedList<Tuple<string, string, byte[]>>();
            foreach (var key in keys)
            {
                GetKeyCodeData(key.Value, out var type, out var data);
                if (data.Length == 0) continue;
                keyLength += data.Length + 10;
                keyDataList.AddLast(new Tuple<string, string, byte[]>(key.Key, type, data));
            }

            var bytes = new byte[sizeof(int)];
            _stream.WriteByte((byte) 'D');

            // Write key length
            bytes.WriteUInt16AsNetworkOrder((ushort) keyLength);
            _stream.Write(bytes, 0, sizeof(ushort));

            // Write start time
            bytes.WriteInt32AsNetworkOrder(startTime);
            _stream.Write(bytes, 0, sizeof(int));

            // Write duration
            bytes.WriteUInt32AsNetworkOrder(duration);
            _stream.Write(bytes, 0, sizeof(uint));

            // ReSharper disable once InvokeAsExtensionMethod for nullable string
            foreach (var ch in StringUtils.TrimOrPadRight(name, 4, ' '))
                _stream.WriteByte((byte) ch);

            bytes.WriteInt16AsNetworkOrder(0);
            _stream.Write(bytes, 0, sizeof(short));

            // Write key count
            _stream.WriteByte((byte) keyDataList.Count);

            foreach (var key in keyDataList)
            {
                // Write key
                // ReSharper disable once InvokeAsExtensionMethod for nullable string
                foreach (var ch in StringUtils.TrimOrPadRight(key.Item1, 4, ' ')) _stream.WriteByte((byte)ch);

                // Write type
                // ReSharper disable once InvokeAsExtensionMethod for nullable string
                foreach (var ch in StringUtils.TrimOrPadRight(key.Item2, 4, ' ')) _stream.WriteByte((byte)ch);

                // Write data length
                bytes.WriteUInt16AsNetworkOrder((ushort) key.Item3.Length);
                _stream.Write(bytes, 0, sizeof(ushort));

                // Write data
                _stream.Write(key.Item3, 0, key.Item3.Length);
            }
            _stream.Flush();
            if (waitForAck) _stream.ReadByte();
        }

        public override void Accept(Timestamped<IMarker> value)
        {
            var mark = value.Value;
            if (value.Value.Label == null)
                SendEvent(mark.Code);
            else 
                SendEvent(mark.Label, mark.Code, false);
        }

        public void Dispose() => Stop();

        private void Start()
        {
            _syncBaseTime = Synchronize();
            _stream.WriteByte((byte)'B'); // start recording
        }

        private void Stop()
        {
            lock (_tcpClient)
                if (_tcpClient.Connected)
                {
                    _stream.WriteByte((byte)'E'); // stop recording
                    _stream.Flush();
                    _stream.ReadByte();
                    Thread.Sleep(500);
                    _stream.WriteByte((byte)'X'); // close
                    _stream.Flush();
                    _stream.ReadByte();
                    _tcpClient.Close();
                }
        }

    }
}
