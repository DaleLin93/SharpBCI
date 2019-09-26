using MarukoLib.Logging;
using System;
using System.Collections.Generic;
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
using SharpBCI.Core.Experiment;

namespace SharpBCI.Paradigms.WebBrowser
{

    internal class WebBrowserAssistantServer
    {

        public class Client
        {

            internal readonly byte[] RawBuffer = new byte[4096];

            public Client(EndPoint endPoint, WebSocketContext context, int priority)
            {
                EndPoint = endPoint;
                Context = context;
                Priority = priority;
            }

            public EndPoint EndPoint { get; }

            public WebSocketContext Context { get; }

            public int Priority { get; }

            public bool IsActived { get; set; }

            public void Disconnect() => Context.WebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disconnect", CancellationToken.None);

        }

        public interface IClientMessageHandler
        {

            void Handle(Client client, IncomingMessage message);

        }

        private static readonly Logger Logger = Logger.GetLogger(typeof(WebBrowserAssistantServer));

        private readonly object _lock = new object();

        private readonly ManualResetEvent _stoppedEvent = new ManualResetEvent(true);

        private readonly LinkedList<IClientMessageHandler> _messageHandlers = new LinkedList<IClientMessageHandler>();

        private readonly LinkedList<Client> _clients = new LinkedList<Client>();

        private readonly WebBrowserAssistantParadigm _paradigm;

        private Thread _listeningThread;

        public WebBrowserAssistantServer(Session session) => _paradigm = (WebBrowserAssistantParadigm) session.Paradigm;

        private static int GetPriorityFromRequest(HttpListenerRequest request)
        { 
            var priorityValues = request.QueryString.GetValues("priority");
            if (priorityValues == null || priorityValues.Length == 0) return 0;
            return int.TryParse(priorityValues[0], out var priority) ? priority : 0;
        }

        private static async Task<IncomingMessage> RetrieveMessage(Client client)
        {
            var result = await client.Context.WebSocket.ReceiveAsync(new ArraySegment<byte>(client.RawBuffer), CancellationToken.None);
            if (result.MessageType == WebSocketMessageType.Close)
                return null;
            if (!result.EndOfMessage)
                throw new Exception("unsupported feature");
            if (result.MessageType != WebSocketMessageType.Text)
                throw new Exception("unsupported websocket message type: " + result.MessageType);
            var json = Encoding.UTF8.GetString(new ArraySegment<byte>(client.RawBuffer, 0, result.Count).ToArray());
            return JsonUtils.Deserialize<IncomingMessage>(json);
        }

        private static async Task SendMessage(Client client, OutgoingMessage message)
        {
            var bytes = Encoding.UTF8.GetBytes(JsonUtils.Serialize(message));
            await client.Context.WebSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
        }

        [CanBeNull]
        public Client ActivedClient 
        {
            get
            {
                Client[] clients;
                lock (_clients)
                    clients = _clients.ToArray();
                Client targetClient = null;
                foreach (var client in clients)
                    if (client.IsActived && (targetClient == null || client.Priority > targetClient.Priority))
                        targetClient = client;
                return targetClient;
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

        public void Start()
        {
            lock (_lock)
            {
                if (_listeningThread?.IsAlive ?? false) return;
                var startedEvent = new ManualResetEvent(false);
                (_listeningThread = new Thread(() =>
                {
                    _stoppedEvent.Reset();
                    startedEvent.Set();
                    try { ConnectionListeningTask(); }
                    catch (ThreadInterruptedException) { }
                    catch (Exception ex) { Logger.Error("Start - listening thread", ex); }
                    finally { _stoppedEvent.Set(); }
                }) {Name = "Web Browser Assistant Server Listener", IsBackground = true}).Start();
                startedEvent.WaitOne();
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

        public void SendMessageToActivedClient(OutgoingMessage message, bool async = true)
        {
            var targetClient = ActivedClient;
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
                var priority = GetPriorityFromRequest(context.Request);
                Logger.Info("OnConnectionEstablished - web client connected", "endPoint", endPoint, "priority", priority);
                new Thread(() => ClientListeningTask(new Client(endPoint, context.AcceptWebSocketAsync(null).Await(), priority)))
                    { Name = $"Web Browser Assistant Socket {endPoint}", IsBackground = true, Priority = ThreadPriority.BelowNormal }.Start();
            }
            else if (uri.LocalPath.TryTrim("/static/", null, out var filePath, StringComparison.Ordinal))
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
                while (true)
                    OnConnectionEstablished(listener.GetContextAsync().Await());
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
                while (true) HandleReceivedMessage(client, RetrieveMessage(client).Await());
            }
            catch (Exception e)
            {
                Logger.Warn("ClientListeningTask - client communication err", e, "endPoint", client.EndPoint);
                lock (_clients) _clients.Remove(client);
            }
        }

        private void HandleReceivedMessage(Client client, IncomingMessage message)
        {
            if (message == null) return;
            Logger.Info("HandleReceivedMessage", "Message", JsonUtils.Serialize(message));
            foreach (var messageHandler in _messageHandlers)
                messageHandler.Handle(client, message);
        }

    }

}
