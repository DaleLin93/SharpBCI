using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Controls;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Devices.MarkerSources;
using SharpBCI.Extensions.Presenters;
using SharpBCI.Extensions.Streamers;

namespace SharpBCI.EGI
{

    /// <summary>
    /// See: https://github.com/Psychtoolbox-3/Psychtoolbox-3/blob/fab0b49fd38ec477e3b4573f23dbd7766b0a89aa/Psychtoolbox/PsychHardware/NetStation.m
    /// </summary>
    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class NetStationEciTagging : StreamConsumer<Timestamped<IMarker>>, IDisposable
    {

        public const string ConsumerName = "Net Station ECI Tagging";

        [ParameterizedObject(typeof(ControlSignalsFactory))]
        public struct TrPulseControlSignals : IParameterizedObject
        {

            private class ControlSignalsFactory : ParameterizedObjectFactory<TrPulseControlSignals>
            {

                private static readonly Parameter<MarkerDefinition?> StartSignal = Parameter<MarkerDefinition?>.CreateBuilder("Start Signal")
                    .SetMetadata(MarkerDefinitionPresenter.NullPlaceholderTextProperty, "Start at the beginning of paradigm")
                    .Build();

                private static readonly Parameter<MarkerDefinition?> StopSignal = Parameter<MarkerDefinition?>.CreateBuilder("Stop Signal")
                    .SetMetadata(MarkerDefinitionPresenter.NullPlaceholderTextProperty, "Stop at the end of paradigm")
                    .Build();

                public override TrPulseControlSignals Create(IParameterDescriptor parameter, IReadonlyContext context)
                    => new TrPulseControlSignals(StartSignal.Get(context), StopSignal.Get(context));

                public override IReadonlyContext Parse(IParameterDescriptor parameter, TrPulseControlSignals controlSignals) => new Context
                {
                    [StartSignal] = controlSignals.StartSignal,
                    [StopSignal] = controlSignals.StopSignal
                };

            }

            public readonly MarkerDefinition? StartSignal, StopSignal;

            public TrPulseControlSignals(MarkerDefinition? startSignal, MarkerDefinition? stopSignal)
            {
                StartSignal = startSignal;
                StopSignal = stopSignal;
            }

        }

        public struct TrPulseGenParams
        {

            public readonly TrPulseControlSignals ControlSignals;

            public readonly TimeSpan Interval;

            public TrPulseGenParams(TrPulseControlSignals controlSignals, TimeSpan interval)
            {
                ControlSignals = controlSignals;
                Interval = interval;
            }

        }

        public class Factory : StreamConsumerFactory<Timestamped<IMarker>>
        {

            public static readonly Parameter<string> IpAddressParam = new Parameter<string>("IP Address", defaultValue: "127.0.0.1");

            public static readonly Parameter<int> PortParam = new Parameter<int>("Port", 55513);

            public static readonly Parameter<ushort> SyncLimitParam = new Parameter<ushort>("Sync Limit", "ms", null, 5);

            public static readonly Parameter<ushort> SyncRetryCountParam = new Parameter<ushort>("Sync Retry Count", 1000);

            public static readonly Parameter<bool> GenerateTrPulseParam = new Parameter<bool>("Generate TR Pulse");

            public static readonly Parameter<TrPulseControlSignals> TrPulseControlSignalsParam = Parameter<TrPulseControlSignals>.CreateBuilder("TR Pulse Control Signals")
                .SetMetadata(ParameterizedObjectPresenter.LayoutOrientationVisibilityProperty, Orientation.Vertical)
                .Build();

            public static readonly Parameter<TimeInterval> TrPulseIntervalParam = new Parameter<TimeInterval>("TR Pulse Interval", defaultValue: new TimeInterval(2, TimeUnit.Second));

            public Factory() : base(IpAddressParam, PortParam, SyncLimitParam, SyncRetryCountParam, GenerateTrPulseParam, TrPulseControlSignalsParam, TrPulseIntervalParam) { }

            public override bool IsVisible(IReadonlyContext context, IDescriptor descriptor)
            { 
                if (ReferenceEquals(descriptor, TrPulseControlSignalsParam) || ReferenceEquals(descriptor, TrPulseIntervalParam)) return GenerateTrPulseParam.Get(context);
                return base.IsVisible(context, descriptor);
            }

            public override IStreamConsumer<Timestamped<IMarker>> Create(Session session, IReadonlyContext context, byte? num)
            {
                TrPulseGenParams? trParams = null;
                if (GenerateTrPulseParam.Get(context)) trParams = new TrPulseGenParams(TrPulseControlSignalsParam.Get(context), TrPulseIntervalParam.Get(context).TimeSpan);
                return new NetStationEciTagging(IPAddress.Parse(IpAddressParam.Get(context)), PortParam.Get(context),
                    TimeSpan.FromMilliseconds(SyncLimitParam.Get(context)), SyncRetryCountParam.Get(context), trParams);
            }

        }

        private readonly TcpClient _tcpClient;

        private readonly Stream _stream;

        private readonly ManualResetEvent _trPulseEvent;

        private long _syncBaseTime;

        private Thread _trPulseThread;

        public NetStationEciTagging(int port, TimeSpan syncLimit, ushort syncRetryCount, TrPulseGenParams? trPulseParams = null) 
            : this(IPAddress.Loopback, port, syncLimit, syncRetryCount, trPulseParams) { }

        public NetStationEciTagging(IPAddress address, int port, TimeSpan syncLimit, ushort syncRetryCount, TrPulseGenParams? trPulseParams = null)
            : this(new IPEndPoint(address, port), syncLimit, syncRetryCount, trPulseParams) { }

        public NetStationEciTagging(IPEndPoint endPoint, TimeSpan syncLimit, ushort syncRetryCount, TrPulseGenParams? trPulseParams = null)
        {
            EndPoint = endPoint ?? throw new ArgumentNullException(nameof(endPoint));
            SyncLimit = syncLimit;
            SyncRetryCount = syncRetryCount;
            TrPulseParams = trPulseParams;
            _tcpClient = Connect(endPoint);
            _stream = _tcpClient.GetStream();
            _trPulseEvent = trPulseParams == null ? null : new ManualResetEvent(false);
            Start();
        }

        ~NetStationEciTagging() => Stop();

        public IPEndPoint EndPoint { get; }

        public TimeSpan SyncLimit { get; }

        public ushort SyncRetryCount { get; }

        public TrPulseGenParams? TrPulseParams { get; }

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
                var resp = (char) stream.ReadByte();
                switch (resp)
                {
                    case 'F':
                        throw new IOException("Connection: ECI error");
                    case 'I':
                        if (stream.ReadByte() != 1) throw new IOException("Connection: Unknown ECI version");
                        break;
                    default:
                        throw new NotSupportedException($"Unknown symbol: '{resp}'");
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
            // TODO: sync with session clock
            var start = DateTimeUtils.CurrentTimeMillis;
            var retry = 0;
            long diff;
            lock (_tcpClient)
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

        public void SendTrEvt(bool waitForAck = false) => SendEvent("TREV", 0, waitForAck);

        public void SendEvent(int code, bool waitForAck = false) => SendEvent(code.ToString().TrimOrPadLeft(4, '0'), code, waitForAck);

        public void SendEvent(string name, int code, bool waitForAck = false) => SendEvent(name, new[] {new KeyValuePair<string, object>("CODE", code)}, waitForAck);

        public void SendEvent(string name, KeyValuePair<string, object>[] keys, bool waitForAck = false) => SendEvent(name, 1, keys, waitForAck);

        public void SendEvent(string name, uint duration, KeyValuePair<string, object>[] keys, bool waitForAck = false) =>
            SendEvent(name, (int) (DateTimeUtils.CurrentTimeMillis - _syncBaseTime), duration, keys, waitForAck);

        public void SendEvent(string name, int startTime, uint duration, KeyValuePair<string, object>[] keys, bool waitForAck = false)
        {
            var bytes = new byte[sizeof(int)];
            var keyLength = 15;
            var keyDataList = new LinkedList<Tuple<string, string, byte[]>>();
            foreach (var key in keys)
            {
                GetKeyCodeData(key.Value, out var type, out var data);
                if (data.Length == 0) continue;
                keyLength += data.Length + 10;
                keyDataList.AddLast(new Tuple<string, string, byte[]>(key.Key, type, data));
            }

            lock (_tcpClient)
            {
                _stream.WriteByte((byte)'D');

                // Write key length
                bytes.WriteUInt16AsNetworkOrder((ushort)keyLength);
                _stream.Write(bytes, 0, sizeof(ushort));

                // Write start time
                bytes.WriteInt32AsNetworkOrder(startTime);
                _stream.Write(bytes, 0, sizeof(int));

                // Write duration
                bytes.WriteUInt32AsNetworkOrder(duration);
                _stream.Write(bytes, 0, sizeof(uint));

                // ReSharper disable once InvokeAsExtensionMethod for nullable string
                foreach (var ch in StringUtils.TrimOrPadRight(name, 4, ' '))
                    _stream.WriteByte((byte)ch);

                bytes.WriteInt16AsNetworkOrder(0);
                _stream.Write(bytes, 0, sizeof(short));

                // Write key count
                _stream.WriteByte((byte)keyDataList.Count);

                foreach (var key in keyDataList)
                {
                    // Write key
                    // ReSharper disable once InvokeAsExtensionMethod for nullable string
                    foreach (var ch in StringUtils.TrimOrPadRight(key.Item1, 4, ' ')) _stream.WriteByte((byte)ch);

                    // Write type
                    // ReSharper disable once InvokeAsExtensionMethod for nullable string
                    foreach (var ch in StringUtils.TrimOrPadRight(key.Item2, 4, ' ')) _stream.WriteByte((byte)ch);

                    // Write data length
                    bytes.WriteUInt16AsNetworkOrder((ushort)key.Item3.Length);
                    _stream.Write(bytes, 0, sizeof(ushort));

                    // Write data
                    _stream.Write(key.Item3, 0, key.Item3.Length);
                }
                _stream.Flush();
                if (waitForAck) _stream.ReadByte();
            }
        }

        public override void Accept(Timestamped<IMarker> value)
        {
            var marker = value.Value;
            var label = marker.Label;
            if (label == null)
                SendEvent(marker.Code);
            else 
                SendEvent(label, marker.Code);
            if (TrPulseParams == null) return;
            var controlSignals = TrPulseParams.Value.ControlSignals;
            var startCode = controlSignals.StartSignal?.Code;
            var stopCode = controlSignals.StopSignal?.Code;
            if (startCode.HasValue && startCode.Value == marker.Code) _trPulseEvent?.Set();
            if (stopCode.HasValue && stopCode.Value == marker.Code) _trPulseEvent?.Reset();
        }

        public void Dispose() => Stop();

        private void Start()
        {
            _syncBaseTime = Synchronize();
            if (TrPulseParams.HasValue)
            {
                if (TrPulseParams.Value.ControlSignals.StartSignal.HasValue)
                    _trPulseEvent?.Reset();
                else 
                    _trPulseEvent?.Set();
                (_trPulseThread = new Thread(TrPulseWorker) { IsBackground = true }).Start();
            }
            lock (_tcpClient) _stream.WriteByte((byte)'B'); // start recording
        }

        private void Stop()
        {
            lock (_tcpClient)
                if (_tcpClient.Connected)
                {
                    _trPulseThread?.Abort();
                    _trPulseThread = null;
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

        private void TrPulseWorker()
        {
            var currentThread = Thread.CurrentThread;
            Debug.Assert(TrPulseParams != null, nameof(TrPulseParams) + " != null");
            var intervalInMillis = (int)TrPulseParams.Value.Interval.TotalMilliseconds;
            var next = DateTimeUtils.CurrentTimeMillis + intervalInMillis;
            while (currentThread == _trPulseThread && _trPulseEvent.WaitOne())
            {
                var now = DateTimeUtils.CurrentTimeMillis;
                /* Send event */
                if (now >= next)
                {
                    next = now + intervalInMillis;
                    SendTrEvt();
                }
                /* Sleep waiting */
                if (next - now > 200) 
                {
                    Thread.Sleep((int)(next - now - 100));
                    now = DateTimeUtils.CurrentTimeMillis;
                }
                /* Spin waiting */
                long diff;
                while ((diff = next - now) < 200 && diff > 10)
                    now = DateTimeUtils.CurrentTimeMillis; 
            }
        }

    }
}
