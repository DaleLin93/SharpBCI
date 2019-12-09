using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using MarukoLib.IO;
using MarukoLib.Lang.Concurrent;
using ToastNotifications;
using ToastNotifications.Lifetime;
using ToastNotifications.Messages;
using ToastNotifications.Position;
using MarukoLib.Logging;
using MarukoLib.Persistence;
using SharpBCI.Core.Experiment;

namespace SharpBCI.Paradigms.MI
{
    internal class MiStimClient
    {

#pragma warning disable 0649
        public enum ControlCommand
        {
            Play, Pause
        }

        public class IncomingMessage
        {

            public DateTime Timestamp;

            public string Type;

            /* Stage */

            public string StimId;

            public string VisualResource;

            public string AuditoryResource;

            public int Duration = 0;

            public bool End = false;

            public bool ProgressBar = false;

            /* Progress */

            public float Progress;

            public bool IsStopCtrl = false;

        }

        public class OutgoingMessage
        {

            public string Type;

            public string StimId;

        }
#pragma warning restore 0649

        private static readonly Logger Logger = Logger.GetLogger(typeof(MiStimClient));
        
        private static readonly Encoding TransmissionEncoding = Encoding.UTF8;

        internal event EventHandler FocusRequested;

        internal event EventHandler<float> ProgressChanged;

        internal event EventHandler<ControlCommand> ControlCommandReceived;

        internal event EventHandler<IncomingMessage> MessageReceived;

        internal event EventHandler<MiStage> StageReceived;

        internal event EventHandler Stopped;

        private readonly object _tcpLock = new object();

        private readonly AtomicBool _stoppedFlag = new AtomicBool(false);

        private readonly Notifier _notifier;

        private readonly TcpClient _tcpClient;

        private readonly Stream _stream;

        private readonly Thread _workerThread;

        public MiStimClient(Session session)
        {
            var paradigm = (MiParadigm) session.Paradigm;
            var config = paradigm.Config;
            var commConfig = config.Comm;
            _notifier = new Notifier(cfg =>
            {
                cfg.DisplayOptions.TopMost = true;
                cfg.PositionProvider = new PrimaryScreenPositionProvider(Corner.BottomLeft, 10, 10);
                cfg.LifetimeSupervisor = new TimeAndCountBasedLifetimeSupervisor(TimeSpan.FromSeconds(5), MaximumNotificationCount.FromCount(5));
                cfg.Dispatcher = session.Dispatcher;
            });
            _tcpClient = new TcpClient();
            var colonIndex = commConfig.ServerAddress.IndexOf(':');
            if (colonIndex == -1)
                throw new ArgumentException($"malformed server address, port must be specified: {commConfig.ServerAddress}");
            var ipAddressPart = commConfig.ServerAddress.Substring(0, colonIndex);
            var portPart = int.Parse(commConfig.ServerAddress.Substring(colonIndex + 1));
            _tcpClient.Connect(new IPEndPoint(IPAddress.Parse(ipAddressPart), portPart));
            if (!_tcpClient.Connected)
                throw new ArgumentException($"failed to connected to server: {commConfig.ServerAddress}");
            _stream = _tcpClient.GetStream();
            (_workerThread = new Thread(DoReadSocket) {IsBackground = true, Name = "MI Stim Client Receiver", Priority = ThreadPriority.AboveNormal}).Start();
        }

        ~MiStimClient() => Close();

        public void SendStartSignal() => SendMessage(new OutgoingMessage { Type = "started" });

        public void SendFocusedSignal() => SendMessage(new OutgoingMessage { Type = "focused" });

        public void SendStimOnSet(string stimId)
        {
            if (stimId != null)
                SendMessage(new OutgoingMessage { Type = "stim_on_set", StimId = stimId });
        }

        public void Close()
        {
            lock (_tcpLock)
                if (_tcpClient.Connected)
                {
                    SendMessage(new OutgoingMessage { Type = "closed" });
                    _tcpClient.Close();
                }
            if (_workerThread.IsAlive && _workerThread != Thread.CurrentThread) _workerThread.Abort();
            if (!_stoppedFlag.Set(true)) Stopped?.Invoke(this, EventArgs.Empty);
        }

        private void SendMessage(OutgoingMessage message)
        {
            var json = JsonUtils.Serialize(message);
            var bytes = TransmissionEncoding.GetBytes(json);
            var lengthBytes = new byte[4];
            lengthBytes.WriteInt32(bytes.Length, Endianness.BigEndian);
            try
            {
                lock (_tcpLock)
                    if (_tcpClient.Connected)
                    {
                        _stream.WriteFully(lengthBytes);
                        _stream.WriteFully(bytes);
                        _stream.Flush();
                    }
            }
            catch (Exception e)
            {
                Logger.Warn("SendMessage", e, "json", json);
            }
        }

        private void DoReadSocket()
        {
            var headerBuf = new byte[4];
            try
            {
                for (;;)
                {
                    lock (_tcpLock)
                        if (!_tcpClient.Connected)
                            break;
                    _stream.ReadFully(headerBuf, 0, 2);
                    if (headerBuf[0] != 1 || headerBuf[1] != 3) continue;
                    _stream.ReadFully(headerBuf, 0, 4);
                    var bodySize = headerBuf.ReadInt32(Endianness.BigEndian);
                    var buf = new byte[bodySize];
                    _stream.ReadFully(buf);
                    string json;
                    try
                    {
                        json = TransmissionEncoding.GetString(buf);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn("DoReadSocket - decoding", e, "length", buf.Length);
                        return;
                    }
                    var timestamp = DateTime.Now;
                    IncomingMessage message;
                    try
                    {
                        message = JsonUtils.Deserialize<IncomingMessage>(json);
                    }
                    catch (Exception e)
                    {
                        Logger.Warn("DoReadSocket - deserialization", e, "json", json);
                        _notifier.ShowWarning($"Malformed json message format:\n {json}");
                        return;
                    }
                    message.Timestamp = timestamp;
                    MessageReceived?.Invoke(this, message);
                    HandleIncomingMessage(message);
                }
            }
            catch (Exception e)
            {
                Logger.Warn("DoReadSocket", e);
                _tcpClient.Close();
            }
            finally
            {
                Close();
            }
        }

        private void HandleIncomingMessage(IncomingMessage message)
        {
            if (message?.Type == null) return;
            switch (message.Type)
            {
                case "stage":
                    if (message.End)
                    {
                        Close();
                        return;
                    }
                    StageReceived?.Invoke(this, new MiStage
                    {
                        StimId = message.StimId,
                        VisualStimulus = MiStage.Stimulus<MiStage.VisualStimulusType>.Parse(message.VisualResource),
                        AuditoryStimulus = MiStage.Stimulus<MiStage.AuditoryStimulusType>.Parse(message.AuditoryResource),
                        Duration = (ulong)Math.Max(message.Duration, 0),
                        IsPreload = message.Duration == -1,
                        ShowProgressBar = message.ProgressBar,
                        DebugInfo = message
                    });
                    break;
                case "progress":
                    ProgressChanged?.Invoke(this, message.Progress);
                    break;
                case "focus":
                    FocusRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case "animation_ctrl":
                    ControlCommandReceived?.Invoke(this, message.IsStopCtrl ? ControlCommand.Pause : ControlCommand.Play);
                    break;
                default:
                    Logger.Warn("HandleIncomingMessage - unsupported message", "type", message.Type);
                    _notifier.ShowWarning($"Unsupported message type: {message.Type}");
                    break;
            }
        }

    }
}
