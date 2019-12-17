using MarukoLib.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using MarukoLib.Persistence;
using MarukoLib.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using SharpBCI.Core.Experiment;

namespace SharpBCI.Paradigms.WebBrowser
{

    internal class WebBrowserAssistantServer : WebBrowserAssistantServer.IClientMessageHandler
    {

        public class ValueChangedEventArgs<T> : EventArgs
        {

            public ValueChangedEventArgs(T oldValue, T newValue)
            {
                OldValue = oldValue;
                NewValue = newValue;
            }

            public T OldValue { get; }

            public T NewValue { get; }

        }

        public class ClientDimensions
        {

            public ClientDimensions(Point windowPosition, Point scrollPosition, Size windowOuterSize, Size windowInnerSize, Size documentSize)
            {
                WindowPosition = windowPosition;
                ScrollPosition = scrollPosition;
                WindowOuterSize = windowOuterSize;
                WindowInnerSize = windowInnerSize;
                DocumentSize = documentSize;
            }

            public Point WindowPosition { get; }

            public Point ScrollPosition { get; }

            public Size WindowOuterSize { get; } 

            public Size WindowInnerSize { get; } 

            public Size DocumentSize { get; }

            public void IsReachBounds(double tolerance, out bool left, out bool top, out bool right, out bool bottom)
            {
                left = ScrollPosition.X <= tolerance;
                top = ScrollPosition.Y <= tolerance;
                right = DocumentSize.Width - ScrollPosition.X - WindowInnerSize.Width <= tolerance;
                bottom = DocumentSize.Height - ScrollPosition.Y - WindowInnerSize.Height <= tolerance;
            }

        }

        public class Client
        {

            internal readonly byte[] RawBuffer = new byte[4096];

            public Client(EndPoint endPoint, WebSocketContext context, string referer, int priority)
            {
                EndPoint = endPoint;
                Context = context;
                Referer = referer;
                Priority = priority;
            }

            public EndPoint EndPoint { get; }

            public WebSocketContext Context { get; }

            public string Referer { get; }

            public int Priority { get; }

            public Scene Scene { get; set; } = Scene.Page;

            public bool IsActive { get; set; }

            public ClientDimensions Dimensions { get; set; }

            public void Disconnect() => Context.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);

        }

        public interface IClientMessageHandler
        {

            void Handle(Client client, IncomingMessage message);

        }

        private static readonly Logger Logger = Logger.GetLogger(typeof(WebBrowserAssistantServer));

        private static readonly JsonSerializerSettings SerializerSettings =
            new JsonSerializerSettings {ContractResolver = new CamelCasePropertyNamesContractResolver()};

        public event EventHandler<ValueChangedEventArgs<Client>> ActiveClientChanged;

        public event EventHandler<Client> ActiveClientDimensionsChanged;

        private readonly object _lock = new object();

        private readonly ManualResetEvent _stoppedEvent = new ManualResetEvent(true);

        private readonly LinkedList<IClientMessageHandler> _messageHandlers = new LinkedList<IClientMessageHandler>();

        private readonly LinkedList<Client> _clients = new LinkedList<Client>();

        private readonly WebBrowserAssistantParadigm _paradigm;

        private Client _activeClient;

        private Thread _listeningThread;

        public WebBrowserAssistantServer(Session session)
        {
            _paradigm = (WebBrowserAssistantParadigm) session.Paradigm;
            _messageHandlers.AddLast(this);
        }

        private static string GetQueryValueFromRequest(HttpListenerRequest request, string key)
        {
            var priorityValues = request.QueryString.GetValues(key);
            if (priorityValues == null || priorityValues.Length == 0) return null;
            return priorityValues[0];
        }

        private static int GetPriorityFromRequest(HttpListenerRequest request)
        {
            var priorityStr = GetQueryValueFromRequest(request, "priority");
            return !string.IsNullOrWhiteSpace(priorityStr) && int.TryParse(priorityStr, out var priority) ? priority : 0;
        }

        private static string GetRefererFromRequest(HttpListenerRequest request) => GetQueryValueFromRequest(request, "referer");

        private static async Task<IncomingMessage> RetrieveMessage(Client client)
        {
            var result = await client.Context.WebSocket.ReceiveAsync(new ArraySegment<byte>(client.RawBuffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close) return null;
            if (!result.EndOfMessage) throw new Exception("unsupported feature");
            if (result.MessageType != WebSocketMessageType.Text) throw new Exception("unsupported websocket message type: " + result.MessageType);
            var content = Encoding.UTF8.GetString(new ArraySegment<byte>(client.RawBuffer, 0, result.Count).ToArray());
            Logger.Debug("RetrieveMessage", "content", content);
            return JsonUtils.Deserialize<IncomingMessage>(content, SerializerSettings);
        }

        private static async Task SendMessage(Client client, OutgoingMessage message)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonUtils.Serialize(message, SerializerSettings));
            await client.Context.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        [CanBeNull]
        public Client ActiveClient 
        {
            get => _activeClient;
            set
            {
                if (_activeClient == value) return;
                var old = _activeClient;
                _activeClient = value;
                ActiveClientChanged?.Invoke(this, new ValueChangedEventArgs<Client>(old, value));
            }
        }

        public void AddMessageHandler(IClientMessageHandler messageHandler)
        {
            lock (_messageHandlers)
                _messageHandlers.AddLast(messageHandler);
        }

        public void RemoveMessageHandler(IClientMessageHandler messageHandler)
        {
            lock (_messageHandlers)
                _messageHandlers.Remove(messageHandler);
        }

        [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
        public void Start()
        {
            using (var startedEvent = new ManualResetEvent(false))
            {
                lock (_lock)
                {
                    if (_listeningThread?.IsAlive ?? false) return;
                    (_listeningThread = new Thread(() =>
                        {
                            _stoppedEvent.Reset();
                            startedEvent.Set();
                            try { ConnectionListeningTask(); }
                            catch (ThreadInterruptedException) { }
                            catch (Exception ex) { Logger.Error("Start - listening thread", ex); }
                            finally { _stoppedEvent.Set(); }
                        })
                        { Name = "Web Browser Assistant Server Listener", IsBackground = true }).Start();
                }
                startedEvent.WaitOne();
                startedEvent.Dispose();
            }
        }

        public void Stop()
        {
            lock (_lock)
                if (_listeningThread?.IsAlive ?? false)
                    _listeningThread.Interrupt();
            lock (_clients)
            {
                foreach (var client in _clients)
                    client.Disconnect();
                _clients.Clear();
            }
        }

        public bool WaitForStop() => _stoppedEvent.WaitOne();

        public bool WaitForStop(TimeSpan timeout) => _stoppedEvent.WaitOne(timeout);

        public void SendMessageToAllClients(OutgoingMessage message, bool async = true)
        {
            Client[] clients;
            lock (_clients)
                clients = _clients.ToArray();
            foreach (var client in clients)
                SendMessageToClient(client, message, async);
        }

        public void SendMessageToActiveClient(OutgoingMessage message, bool async = true)
        {
            var targetClient = ActiveClient;
            if (targetClient == null) return;
            SendMessageToClient(targetClient, message, async);
        }

        public void SendMessageToClient(Client client, OutgoingMessage message, bool async = true)
        {
            var task = SendMessage(client, message);
            if (!async) task.Await();
        }
        
        private void OnConnectionEstablished(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            var endPoint = context.Request.RemoteEndPoint;
            var uri = request.Url;
            Logger.Info("OnConnectionEstablished", "uri", uri, "endPoint", endPoint);
            if ("/".Equals(uri.LocalPath, StringComparison.Ordinal))
            {
                var referer = GetRefererFromRequest(request);
                var priority = GetPriorityFromRequest(request);
                Logger.Info("OnConnectionEstablished - web client connected", "endPoint", endPoint, "referer", referer, "priority", priority);
                new Thread(() => ClientListeningTask(new Client(endPoint, context.AcceptWebSocketAsync(null).Await(), referer, priority)))
                    { Name = $"Web Browser Assistant Socket {endPoint}", IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
            }
            else if (uri.LocalPath.TryTrim("/static/", null, out var filePath))
            {
                filePath = filePath.Replace('/', '\\');
                filePath = Path.Combine(_paradigm.Config.User.WebRootDir, filePath);
                if (!File.Exists(filePath))
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                    response.Close();
                    return;
                }
                byte[] fileBytes;
                try
                {
                    fileBytes = File.ReadAllBytes(filePath);
                }
                catch (Exception e)
                {
                    Logger.Warn("OnConnectionEstablished - failed to read file", e, "filePath", filePath);
                    response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    response.Close();
                    return;
                }
                response.Headers.Add("Access-Control-Allow-origin", "*");
                response.StatusCode = (int)HttpStatusCode.OK;
                response.ContentType = System.Web.MimeMapping.GetMimeMapping(Path.GetFileName(filePath));
                response.ContentLength64 = fileBytes.Length;
                response.OutputStream.WriteFully(fileBytes);
                response.Close();
            }
            else
            {
                response.StatusCode = (int)HttpStatusCode.NotFound;
                response.Close();
            }
        }

        private void ConnectionListeningTask()
        {
            var listeningUriPrefix = $"http://127.0.0.1:{_paradigm.Config.System.ListeningPort}/";
            var listener = new HttpListener { AuthenticationSchemes = AuthenticationSchemes.Anonymous };
            listener.Prefixes.Add(listeningUriPrefix);
            listener.Start();
            Logger.Info("ConnectionListeningTask - listening started", "uri", listeningUriPrefix);
            try
            {
                for (;;) OnConnectionEstablished(listener.GetContextAsync().Await());
            }
            finally
            {
                listener.Stop();
            }
        }

        private void ClientListeningTask(Client client)
        {
            lock (_clients) _clients.AddLast(client);
            try
            {
                while (client.Context.WebSocket.State == WebSocketState.Open || client.Context.WebSocket.State == WebSocketState.Connecting)
                    HandleReceivedMessage(client, RetrieveMessage(client).Await());
            }
            catch (Exception e)
            {
                Logger.Warn("ClientListeningTask - client communication err", e, "endPoint", client.EndPoint);
            }
            finally
            {
                client.IsActive = false;
                UpdateActiveClient();
                lock (_clients) _clients.Remove(client);
            }
        }

        private void HandleReceivedMessage(Client client, IncomingMessage message)
        {
            if (message == null) return;
            foreach (var messageHandler in _messageHandlers)
                messageHandler.Handle(client, message);
        }

        void IClientMessageHandler.Handle(Client client, IncomingMessage message)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (message.Type)
            {
                case "Dimensions":
                    client.Dimensions = new ClientDimensions(
                        message.WindowPosition ?? default, message.ScrollPosition ?? default,
                        message.WindowOuterSize ?? default, message.WindowInnerSize ?? default,
                        message.DocumentSize ?? default);
                    if (client == ActiveClient) ActiveClientDimensionsChanged?.Invoke(this, client);
                    break;
                case "Focus":
                    client.IsActive = message.Focused ?? true;
                    UpdateActiveClient();
                    break;
                case "Scene":
                    client.Scene = message.Scene ?? Scene.Page;
                    break;
            }
        }

        private void UpdateActiveClient()
        {
            Client[] clients;
            lock (_clients)
                clients = _clients.ToArray();
            Client targetClient = null;
            foreach (var client in clients)
                if (client.IsActive && (targetClient == null || client.Priority > targetClient.Priority))
                    targetClient = client;
            ActiveClient = targetClient;
        }

    }

}
