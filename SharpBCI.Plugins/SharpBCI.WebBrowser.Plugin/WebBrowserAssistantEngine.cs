using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Accord.Math;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.Logging;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.IO.Devices.BiosignalSources;
using SharpBCI.Extensions.IO.Devices.EyeTrackers;
using SharpBCI.Extensions.IO.Devices.MarkerSources;
using SharpBCI.Extensions.Patterns;
using SharpBCI.Paradigms.Speller;
using SharpBCI.Paradigms.Speller.SSVEP;

namespace SharpBCI.Paradigms.WebBrowser
{

    internal class ModeOnSetInterceptor : Core.IO.Filter<Timestamped<IMarker>>
    {

        public event EventHandler<Mode> ModeOnSet;

        public override bool Accept(Timestamped<IMarker> value)
        {
            switch (value.Value.Code)
            {
                case WebBrowserAssistantParadigm.NormalModeOnSetMarker:
                    ModeOnSet?.Invoke(this, Mode.Normal);
                    return false;
                case WebBrowserAssistantParadigm.ReadingModeOnSetMarker:
                    ModeOnSet?.Invoke(this, Mode.Reading);
                    return false;
                default:
                    return true;
            }
        }

    }

    internal class GazePointProvider : Core.IO.Consumer<Timestamped<IGazePoint>>
    {

        private static readonly Supplier<double> GraphicsScale = 
            Suppliers.MemoizeWithExpiration(() => GraphicsUtils.Scale, TimeSpan.FromSeconds(5));

        public Point? CurrentPosition { get; private set; }

        public bool TryGetCurrentPosition(out Point point)
        {
            var position = CurrentPosition;
            if (!position.HasValue)
            {
                point = default;
                return false;
            }
            point = position.Value;
            return true;
        }

        public override void Accept(Timestamped<IGazePoint> value)
        {
            var gazePoint = value.Value;
            var scale = GraphicsScale();
            CurrentPosition = new Point {X = Math.Round(gazePoint.X / scale), Y = Math.Round(gazePoint.Y / scale)};
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
            _cancellable = userConfig.CancelByMovement;
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

        private const int SsvepDelayMillis = 140;

        private static readonly Logger Logger = Logger.GetLogger(typeof(WebBrowserAssistantEngine));

        private static readonly string[] IndicatorBorderThicknesses = { "2px" };

        private static readonly string[] IndicatorBorderStyles = { "double", "dotted", "dashed", "groove" };

        [SuppressMessage("ReSharper", "StringLiteralTypo")] 
        private static readonly string[] VisualColors = { "deepskyblue", "red", "orange", "lightgreen" };

        private readonly object _modeLock = new object();

        private readonly ManualResetEvent _hasActiveClient = new ManualResetEvent(false);

        private readonly ManualResetEvent _trialStartEvent = new ManualResetEvent(false);

        private readonly ManualResetEvent _trialCancelled = new ManualResetEvent(false);

        private readonly WebBrowserAssistantParadigm _paradigm;

        private readonly WebBrowserAssistantServer _server;

        private readonly MarkerStreamer _markerStreamer;

        private readonly GazePointStreamer _gazePointStreamer;

        private readonly BiosignalStreamer _biosignalStreamer;

        private readonly ModeOnSetInterceptor _modeOnSetInterceptor;

        private readonly GazePointProvider _gazePointProvider;

        private readonly DwellTrialController _dwellTrialController;

        private readonly ISsvepIdentifier _navigatingSsvepDetector;

        private readonly ISsvepIdentifier _spellingSsvepDetector;

        private readonly int _frequencyCount;

        private Mode _mode = Mode.Normal;

        private Thread _thread;

        public WebBrowserAssistantEngine(Session session)
        {
            var paradigm = _paradigm = (WebBrowserAssistantParadigm) session.Paradigm;

            if (!session.StreamerCollection.TryFindFirst(out _markerStreamer)) throw new UserException("Marker streamer not found");
            if (!session.StreamerCollection.TryFindFirst(out _gazePointStreamer)) throw new UserException("Gaze-point streamer not found");
            if (!session.StreamerCollection.TryFindFirst(out _biosignalStreamer)) throw new UserException("Biosignal streamer not found");

            _server = new WebBrowserAssistantServer(session);
            _server.AddMessageHandler(this);
            _server.ActiveClientChanged += (sender, e) =>
            {
                if (e.OldValue == null)
                    _hasActiveClient.Set();
                else
                {
                    if (e.NewValue == null) _hasActiveClient.Reset();
                    InterruptTrial();
                }
            };
            _server.ClientDimensionsChanged += (sender, e) =>
            {
                if (e.IsActiveClient) InterruptTrial();
            };
            _server.ClientSceneChanged += (sender, e) =>
            {
                if (e.IsActiveClient) InterruptTrial();
            };

            _modeOnSetInterceptor = new ModeOnSetInterceptor();
            _modeOnSetInterceptor.ModeOnSet += (sender, e) => Mode = e;

            _gazePointProvider = new GazePointProvider();
            _dwellTrialController = new DwellTrialController(session.Clock, _gazePointProvider, paradigm.Config.User);
            _dwellTrialController.Triggered += (sender, e) => _trialStartEvent.Set();
            _dwellTrialController.Cancelled += (sender, e) => _trialCancelled.Set();

            var stimulationScheme = paradigm.Config.User.StimulationScheme;
            var samplingRate = _biosignalStreamer.BiosignalSource.Frequency;
            var frequencies = stimulationScheme.Frequencies;
            var interFrequencies = new float[frequencies.Count - 1];
            for (var i = 0; i < interFrequencies.Length; i++) interFrequencies[i] = (frequencies[i] + frequencies[i + 1]) / 2;
            var frequencyPatterns = frequencies.Concat(interFrequencies).Select(f => new CompositeTemporalPattern<SinusoidalPattern>(new SinusoidalPattern(f))).ToArray();
            var bandMixingParams = new SubBandMixingParams(1.25, 0.25);
            var channels = paradigm.Config.System.Channels.Enumerate(1, _biosignalStreamer.BiosignalSource.ChannelNum).Select(i => (uint) (i - 1)).ToArray();
            _navigatingSsvepDetector = new HybridSsvepIdentifier(session.Clock, 4, frequencyPatterns, stimulationScheme.FilterBank, bandMixingParams,
                stimulationScheme.HarmonicCount, 0, channels, samplingRate, paradigm.Config.User.StimulationDuration.Navigating, 0);
            _spellingSsvepDetector = new HybridSsvepIdentifier(session.Clock, 4, frequencyPatterns, stimulationScheme.FilterBank, bandMixingParams, 
                stimulationScheme.HarmonicCount, 0, channels, samplingRate, paradigm.Config.User.StimulationDuration.Spelling, 0);
            _frequencyCount = stimulationScheme.Frequencies.Count;
        }

        ~WebBrowserAssistantEngine()
        {
            _hasActiveClient.Dispose();
            _trialStartEvent.Dispose();
            _trialCancelled.Dispose();
        }

        public Mode Mode
        {
            get
            {
                lock (_modeLock)
                    return _mode;
            }
            set
            {
                lock (_modeLock)
                {
                    if (_mode == value) return;
                    _mode = value;
                }
                _server.SendMessageToAllClients(new OutgoingMessage {Type = "Mode", Mode = value});
            }
        }

        public void Start()
        {
            _server.Start();
            _dwellTrialController.Start();

            _navigatingSsvepDetector.IsActive = false;
            _spellingSsvepDetector.IsActive = false;

            _markerStreamer.AttachFilter(_modeOnSetInterceptor);
            _biosignalStreamer.AttachConsumer(_navigatingSsvepDetector);
            _biosignalStreamer.AttachConsumer(_spellingSsvepDetector);
            _gazePointStreamer.AttachConsumer(_gazePointProvider);

            (_thread = new Thread(RunTrials) {IsBackground = true}).Start();
        }

        public void SwitchMode()
        {
            var array = typeof(Mode).GetEnumValues().Cast<Mode>().ToArray();
            Mode = array[(array.IndexOf(Mode) + 1) % array.Length];
        }

        public void Stop()
        {
            _navigatingSsvepDetector.IsActive = false;
            _spellingSsvepDetector.IsActive = false;

            _markerStreamer.DetachFilter(_modeOnSetInterceptor);
            _biosignalStreamer.DetachConsumer(_navigatingSsvepDetector);
            _biosignalStreamer.DetachConsumer(_spellingSsvepDetector);
            _gazePointStreamer.DetachConsumer(_gazePointProvider);

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
                try
                {
                    if (_hasActiveClient.WaitOne())
                    {
                        var client = _server.ActiveClient;
                        if (client == null) continue;
                        RunTrial(client);
                    }
                }
                catch (ThreadAbortException) { return; }
                catch (ThreadInterruptedException) { }
                catch (Exception e) { Logger.Error("RunTrials", e); }
            }
        }

        private void RunTrial([NotNull] WebBrowserAssistantServer.Client client)
        {
            var scene = client.Scene;
            var dimensions = client.Dimensions;
            var detector = scene == Scene.Keyboard ? _spellingSsvepDetector : _navigatingSsvepDetector;
            var edgeScrolling = _paradigm.Config.User.EnableEdgeScrolling && dimensions != null;
            var edgeScrollHotAreaSize = _paradigm.Config.User.EdgeScrollingHotAreaSize;
            var edgeScrollSpeed = _paradigm.Config.User.EdgeScrollingSpeed;

            /* Initialize signals. */
            _dwellTrialController.Reset();
            _trialStartEvent.Reset();
            _trialCancelled.Reset();

            /* Waiting for the start signal. */
            Point gazePoint;
            if (edgeScrolling)
                while (!_trialStartEvent.WaitOne(TimeSpan.FromMilliseconds(20)))
                {
                    if (!_gazePointProvider.TryGetCurrentPosition(out gazePoint)) continue;
                    var area = GetGazeArea(dimensions, edgeScrollHotAreaSize, gazePoint);
                    if (area == 0) continue;
                    var verticalScrollDistance = dimensions.WindowInnerSize.Height * edgeScrollSpeed;
                    SendScroll(client, new Point {X = 0, Y = area * verticalScrollDistance});
                }
            else
                _trialStartEvent.WaitOne();
            if (!_gazePointProvider.TryGetCurrentPosition(out gazePoint)) return;
            _server.SendMessageToClient(client, new OutgoingMessage { Type = "StartTrial", GazePoint = gazePoint });
            
            var cancelled = true;
            detector.IsActive = true;
            try
            {
                /* Waiting for the ending of trial or cancelled by system. */
                var stimulationMillis = (int) (detector.WindowSizeInSecs * 1000);
                cancelled = _trialCancelled.WaitOne(stimulationMillis + SsvepDelayMillis);
                if (cancelled) return;
                _server.SendMessageToClient(client, new OutgoingMessage {Type = "EndTrial"});
            }
            finally
            {
                detector.IsActive = false;
                if (cancelled) SendIdentifiedFrequency(client, AbsentFrequencyIndex);
            }

            /* Send identified frequency index. */
            var result = detector.Identify();
            SendIdentifiedFrequency(client, result.State == IdentificationState.Success ? result.FrequencyIndex : AbsentFrequencyIndex);
            Logger.Info("RunTrial", "result", result);
        }

        private void InterruptTrial() => _thread?.Interrupt();

        private int GetGazeArea(WebBrowserAssistantServer.ClientDimensions dimensions, Rectangle hotAreaSize, Point gazePoint)
        {
            var x = gazePoint.X - dimensions.WindowPosition.X;
            var y = gazePoint.Y - dimensions.WindowPosition.Y;
            var horizontalSpacing = dimensions.WindowOuterSize.Width * (1 - hotAreaSize.Width) / 2;
            if (x < horizontalSpacing || x >= dimensions.WindowOuterSize.Width - horizontalSpacing) return 0;
            var contentOffset = dimensions.WindowOuterSize.Height * hotAreaSize.Height;
            var contentHeight = dimensions.WindowOuterSize.Height - contentOffset * 2;
            if (y >= contentOffset && y <= contentHeight + contentOffset) return 0;
            dimensions.IsReachBounds(3, out _, out var topReached, out _, out var bottomReached);
            return y < contentOffset ? topReached ? 0 : -1 : bottomReached ? 0 : +1;
        }

        private void SendIdentifiedFrequency([NotNull] WebBrowserAssistantServer.Client client, int frequencyIndex)
        {
            if (frequencyIndex < 0 || frequencyIndex >= _frequencyCount) frequencyIndex = AbsentFrequencyIndex;
            _server.SendMessageToClient(client, new OutgoingMessage {Type = "Frequency", FrequencyIndex = frequencyIndex});
        }

        private void SendScroll([NotNull] WebBrowserAssistantServer.Client client, Point scrollDistance) =>
            _server.SendMessageToClient(client, new OutgoingMessage {Type = "Scroll", ScrollDistance = scrollDistance});
        
        void WebBrowserAssistantServer.IClientMessageHandler.Handle(WebBrowserAssistantServer.Client client, IncomingMessage message)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (message.Type)
            {
                case "Handshake":
                    var frequencies = _paradigm.Config.User.StimulationScheme.Frequencies;
                    var schemes = new OutgoingMessage.VisualScheme[frequencies.Count];
                    for (var i = 0; i < frequencies.Count; i++)
                    {
                        schemes[i].Frequency = frequencies[i];
                        schemes[i].BorderThickness = IndicatorBorderThicknesses[i % IndicatorBorderThicknesses.Length];
                        schemes[i].BorderStyle = IndicatorBorderStyles[i % IndicatorBorderStyles.Length];
                        schemes[i].Color = VisualColors[i % VisualColors.Length];
                    }
                    _server.SendMessageToClient(client, new OutgoingMessage
                    {
                        Type = "Handshake", Debug = _paradigm.Config.System.DebugInformation, 
                        HomePage = _paradigm.Config.User.HomePage,
                        VisualSchemes = schemes,
                        MaxActiveDistance = _paradigm.Config.User.CursorMovementTolerance,
                        ConfirmationDelay = _paradigm.Config.User.ConfirmationDelay,
                        EdgeScrolling = _paradigm.Config.User.EnableEdgeScrolling,
                        StimulationSize = new Point
                        {
                            X = _paradigm.Config.User.StimulationSize.Width,
                            Y = _paradigm.Config.User.StimulationSize.Height
                        },
                        Mode = Mode
                    });
                    break;
                case "Mode":
                    Mode = message.Mode ?? Mode.Normal;
                    break;
            }
        }

    }
}
