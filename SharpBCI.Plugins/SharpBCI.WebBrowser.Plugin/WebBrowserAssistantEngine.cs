using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Threading;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.Logging;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Devices.EyeTrackers;
using SharpBCI.Extensions.Patterns;
using SharpBCI.Extensions.Streamers;

namespace SharpBCI.Paradigms.WebBrowser
{

    internal class GazePointProvider : StreamConsumer<Timestamped<IGazePoint>>
    {

        public Point? CurrentPosition { get; private set; }

        public override void Accept(Timestamped<IGazePoint> value)
        {
            var gazePoint = value.Value;
            var scale = GraphicsUtils.Scale;
            CurrentPosition = new Point((int)Math.Round(gazePoint.X / scale), (int)Math.Round(gazePoint.Y / scale));
        }

    }

    internal class DwellTrialController
    {

        public event EventHandler Triggered;

        public event EventHandler Cancelled;

        private readonly IClock _clock;

        private readonly GazePointProvider _gazePointProvider;

        private readonly bool _cancellable;

        private readonly uint _cursorMovementTolerance;

        private readonly uint _dwellToSelectDelay;

        private Timer _timer;

        private Timestamped<Point>? _point;

        private bool _inTrial;

        public DwellTrialController(IClock clock, GazePointProvider gazePointProvider, 
            WebBrowserAssistantParadigm.Configuration.UserConfig userConfig)
        {
            _clock = clock;
            _gazePointProvider = gazePointProvider;
            _cancellable = userConfig.TrialCancellable;
            _cursorMovementTolerance = userConfig.CursorMovementTolerance;
            _dwellToSelectDelay = userConfig.DwellSelectionDelay;

            Reset();
        }

        public void Start()
        {
            _timer?.Dispose();
            Reset();
            _timer = new Timer(Tick, null, 100, 100);
        }

        public void Stop()
        {
            _timer?.Dispose();
            _timer = null;
        }

        public void Reset()
        {
            _point = null;
            _inTrial = false;
        }

        public void Dispose() => Stop();

        private void Tick(object state)
        {
            var position = _gazePointProvider.CurrentPosition;
            if (position == null) return;
            var pNew = position.Value;
            var now = _clock.Time;
            if (_point == null)
            {
                _point = new Timestamped<Point>(now, pNew);
                return;
            }
            var pOld = _point.Value.Value;
            if (Math.Abs(pOld.X - pNew.X) + Math.Abs(pOld.Y - pNew.Y) > _cursorMovementTolerance)
            {
                _point = new Timestamped<Point>(now, pNew);
                if (_cancellable && _inTrial) Cancelled?.Invoke(this, EventArgs.Empty);
                return;
            }
            if (_inTrial || _clock.Unit.ToMilliseconds(Math.Abs(now - _point.Value.Timestamp)) < _dwellToSelectDelay) return;
            _point = new Timestamped<Point>(now, pNew);
            _inTrial = true;
            Triggered?.Invoke(this, EventArgs.Empty);
        }

    }

    internal class WebBrowserAssistantEngine : WebBrowserAssistantServer.IClientMessageHandler
    {

        private const int AbsentFrequencyIndex = -1;

        private static readonly Logger Logger = Logger.GetLogger(typeof(WebBrowserAssistantEngine));

        private static readonly float[] Frequencies = { 13, 14, 15, 16 };

        private static readonly string[] IndicatorBorderThicknesses = { "2px" };

        private static readonly string[] IndicatorBorderStyles = { "double", "dotted", "dashed", "groove" };

        private static readonly string[] VisualColors = { "deepskyblue", "red", "orange", "lightgreen" };

        private static readonly float[] ExtraFrequencies = { 13.5F, 14.5F, 15.5F };

        private readonly ManualResetEvent _trialStartEvent = new ManualResetEvent(false);

        private readonly ManualResetEvent _trialCancelled = new ManualResetEvent(false);

        private readonly WebBrowserAssistantParadigm _paradigm;

        private readonly WebBrowserAssistantServer _server;

        private readonly GazePointStreamer _gazePointStreamer;

        private readonly BiosignalStreamer _biosignalStreamer;

        private readonly GazePointProvider _gazePointProvider;

        private readonly DwellTrialController _dwellTrialController;

        private readonly SsvepDetector _ssvepDetector;

        private Thread _thread;

        public WebBrowserAssistantEngine(Session session)
        {
            var paradigm = _paradigm = (WebBrowserAssistantParadigm) session.Paradigm;

            _server = new WebBrowserAssistantServer(session);
            _server.AddMessageHandler(this);
            if (!session.StreamerCollection.TryFindFirst(out _gazePointStreamer)) throw new UserException("Gaze-point streamer not found");
            if (!session.StreamerCollection.TryFindFirst(out _biosignalStreamer)) throw new UserException("Biosignal streamer not found");
            _gazePointProvider = new GazePointProvider();
            _dwellTrialController = new DwellTrialController(session.Clock, _gazePointProvider, paradigm.Config.User);
            _dwellTrialController.Triggered += (sender, e) => _trialStartEvent.Set();
            _dwellTrialController.Cancelled += (sender, e) => _trialCancelled.Set();

            _ssvepDetector = new SsvepDetector(session.Clock, 4, Frequencies.Concat(ExtraFrequencies)
                    .Select(f => new CompositeTemporalPattern<SinusoidalPattern>(new SinusoidalPattern(f))).ToArray(), new[]
                {
                    new SsvepDetector.BandpassFilter(12, 90),
                    new SsvepDetector.BandpassFilter(24, 90)
                }, new SsvepDetector.FbccaSubBandMixingParams(1.25, 0.25), 2, 0,
                paradigm.Config.System.Channels.Enumerate(1, _biosignalStreamer.BiosignalSource.ChannelNum).Select(i => (uint)(i - 1)).ToArray(),
                _biosignalStreamer.BiosignalSource.Frequency, paradigm.Config.User.TrialDuration, 0);
        }

        public void Start()
        {
            _server.Start();
            _dwellTrialController.Start();

            _ssvepDetector.Actived = true;

            _biosignalStreamer.Attach(_ssvepDetector);
            _gazePointStreamer.Attach(_gazePointProvider);

            (_thread = new Thread(RunTrials) {IsBackground = true}).Start();
        }

        public void Stop()
        {
            _ssvepDetector.Actived = false;

            _biosignalStreamer.Detach(_ssvepDetector);
            _gazePointStreamer.Detach(_gazePointProvider);

            _dwellTrialController.Stop();
            _server.Stop();

            _thread?.Abort();
        }

        public bool WaitForStop() => _server.WaitForStop();

        public bool WaitForStop(TimeSpan timeout) => _server.WaitForStop(timeout);

        [SuppressMessage("ReSharper", "FunctionNeverReturns")]
        private void RunTrials()
        {
            for (;;)
            {
                _dwellTrialController.Reset();
                _trialStartEvent.Reset();
                _trialCancelled.Reset();

                /* Waiting for the start signal. */
                _trialStartEvent.WaitOne();
                var point = _gazePointProvider.CurrentPosition;
                if (point == null) continue;
                var gazePoint = new OutgoingMessage.Point {X = point.Value.X, Y = point.Value.Y};
                _server.SendMessageToAllClients(new OutgoingMessage { Type = "StartTrial", GazePoint = gazePoint});

                /* Waiting for the ending of trial or cancelled by system. */
                var cancelled = _trialCancelled.WaitOne(TimeSpan.FromMilliseconds((int)(_paradigm.Config.User.TrialDuration + 200)));

                if (!cancelled) _server.SendMessageToAllClients(new OutgoingMessage { Type = "EndTrial" });

                /* Send identified frequency index, absent on cancelled trial. */
                var frequencyIndex = cancelled ? AbsentFrequencyIndex : _ssvepDetector.Classify();
                Logger.Info("StageProgram_StageChanged", "classifiedFrequencyIndex", frequencyIndex);
                if (frequencyIndex < 0 || frequencyIndex >= Frequencies.Length)
                    frequencyIndex = AbsentFrequencyIndex;
                _server.SendMessageToAllClients(new OutgoingMessage { Type = "Frequency", FrequencyIndex = frequencyIndex });
            }
        }

        void WebBrowserAssistantServer.IClientMessageHandler.Handle(WebBrowserAssistantServer.Client client, IncomingMessage message)
        {
            switch (message.Type)
            {
                case "Handshake":
                    var schemes = new OutgoingMessage.VisualScheme[Frequencies.Length];
                    for (var i = 0; i < Frequencies.Length; i++)
                    {
                        schemes[i].Frequency = Frequencies[i];
                        schemes[i].BorderThickness = IndicatorBorderThicknesses[i % IndicatorBorderThicknesses.Length];
                        schemes[i].BorderStyle = IndicatorBorderStyles[i % IndicatorBorderStyles.Length];
                        schemes[i].Color = VisualColors[i % VisualColors.Length];
                    }
                    _server.SendMessageToClient(client, new OutgoingMessage
                    {
                        Type = "Handshake", Debug = _paradigm.Config.System.DebugInformation, 
                        HomePage = _paradigm.Config.User.HomePage?.ToString(),
                        VisualSchemes = schemes,
                        MaxActiveDistance = _paradigm.Config.User.CursorMovementTolerance,
                        ConfirmationDelay = _paradigm.Config.User.ConfirmationDelay,
                        StimulationSize = new OutgoingMessage.Point
                        {
                            X = _paradigm.Config.User.StimulationSize.Width,
                            Y = _paradigm.Config.User.StimulationSize.Height
                        }
                    });
                    break;
                case "Focus":
                    client.IsActived = message.Focused ?? true;
                    break;
                default:
                    Logger.Warn("Handle - unknown message type", "messageType", message.Type);
                    break;
            }
        }

    }
}
