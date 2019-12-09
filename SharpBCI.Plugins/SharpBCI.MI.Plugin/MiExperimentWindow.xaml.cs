using SharpBCI.Core.Staging;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System;
using System.Speech.Synthesis;
using System.Windows.Media.Imaging;
using WpfAnimatedGif;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using SharpBCI.Core.IO;
using System.Windows.Interop;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.Logging;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using Image = System.Windows.Controls.Image;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using SharpBCI.Extensions.IO.Devices.EyeTrackers;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;
using System.IO;

namespace SharpBCI.Paradigms.MI
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for MiExperimentWindow.xaml
    /// </summary>
    internal partial class MiExperimentWindow
    {

        private static readonly Logger Log = Logger.GetLogger(typeof(MiExperimentWindow));

        private class GazeFocusDetector : Core.IO.Consumer<Timestamped<IGazePoint>>
        {

            public struct GazeHitTest
            {

                public readonly long Time;

                public readonly bool Hit;

                public GazeHitTest(long time, bool hit)
                {
                    Time = time;
                    Hit = hit;
                }

            }

            private const double MinimumInsideRatio = .8;

            private const int MinimumGazePointCount = 20;

            private static readonly IContainer<double> UserInterfaceScale =
                new Memoized<double>(() => GraphicsUtils.Scale, MarukoLib.Lang.Clock.SystemMillisClock, 2000);

            public event EventHandler Focused, Enter, Leave;

            private readonly LinkedList<GazeHitTest> _gazeHitTests = new LinkedList<GazeHitTest>();

            private bool _enabled, _entered;

            public GazeFocusDetector(IClock clock, uint t)
            {
                Clock = clock;
                T = t;
            }

            public IClock Clock { get; }

            /// <summary>
            /// Time window length.
            /// </summary>
            public uint T { get; }

            public Point Position { get; private set; }

            public double Radius { get; private set; }

            public long StartTimestamp { get; private set; }

            public bool IsEnabled
            {
                get => _enabled;
                set
                {
                    _entered = false;
                    _gazeHitTests.Clear();
                    if (value) StartTimestamp = Clock.Time;
                    _enabled = value;
                }
            }

            public void SetTargetCircle(Point position, double radius)
            {
                Position = position;
                Radius = Math.Abs(radius);
            }

            public override void Accept(Timestamped<IGazePoint> value)
            {
                if (!IsEnabled || value.Value == null) return;
                var now = Clock.Time;
                var inside = IsInsideTargetCircle(value.Value);
                if (_entered != inside)
                {
                    (inside ? Enter : Leave)?.Invoke(this, EventArgs.Empty);
                    _entered = inside;
                }
                _gazeHitTests.AddLast(new GazeHitTest(now, inside));
                while (_gazeHitTests.Count > 0 && _gazeHitTests.First.Value.Time < now - T) _gazeHitTests.RemoveFirst();
                if (StartTimestamp + T > now) return;
                if (_gazeHitTests.Count < MinimumGazePointCount) return;
                if (_gazeHitTests.Count(g => g.Hit) / (double)_gazeHitTests.Count < MinimumInsideRatio) return;
                Focused?.Invoke(this, EventArgs.Empty);
                IsEnabled = false;
            }

            private bool IsInsideTargetCircle(IGazePoint gazePoint)
            {
                var scale = UserInterfaceScale.Value;
                var xDiff = gazePoint.X / scale - Position.X;
                var yDiff = gazePoint.Y / scale - Position.Y;
                return Math.Sqrt(xDiff * xDiff + yDiff * yDiff) < Radius;
            }

        }

        private interface IVisualElement
        {

            void Show();

            void Hide();

            void Play();

            void Pause();

            void Rewind();

            void Restart();

        }

        private interface IAuditoryElement
        {

            void Play();

            void Stop();

            void Restart();

        }

        private interface IVisualStimulusPresenter
        {

            bool Accept(MiStage.VisualStimulusType stimulusType);

            IVisualElement Present(string content, bool preload);

        }

        private interface IAuditoryStimulusPresenter
        {

            bool Accept(MiStage.AuditoryStimulusType stimulusType);

            IAuditoryElement Present(string content, bool preload);

        }

        private class VisualTextStimulusPresenter : IVisualStimulusPresenter
        {

            private class Element : IVisualElement
            {

                public readonly TextBlock TextBlock;

                public Element(TextBlock textBlock) => TextBlock = textBlock;

                public void Show() => TextBlock.Visibility = Visibility.Visible;

                public void Hide() => TextBlock.Visibility = Visibility.Hidden;

                public void Play() { }

                public void Pause() { }

                public void Rewind() { }

                public void Restart() { }

            }

            private readonly TextBlock _textBlock;

            private readonly Element _textElement;

            public VisualTextStimulusPresenter(TextBlock textBlock)
            {
                _textBlock = textBlock;
                _textElement = new Element(textBlock);
            }

            public bool Accept(MiStage.VisualStimulusType stimulusType) => stimulusType == MiStage.VisualStimulusType.Text;

            public IVisualElement Present(string content, bool repeat)
            {
                _textBlock.Text = content;
                return _textElement;
            }

        }

        private class VisualImageStimulusPresenter : IVisualStimulusPresenter
        {

            private class Element : IVisualElement
            {

                public Image Control;

                public void Show() => Control.Visibility = Visibility.Visible;

                public void Hide() => Control.Visibility = Visibility.Hidden;

                public void Play()
                {
                    var controller = ImageBehavior.GetAnimationController(Control);
                    if (controller != null && controller.FrameCount > 1) controller.Play();
                }

                public void Pause()
                {
                    var controller = ImageBehavior.GetAnimationController(Control);
                    if (controller != null && controller.FrameCount > 1) controller.Pause();
                }

                public void Rewind()
                {
                    var controller = ImageBehavior.GetAnimationController(Control);
                    if (controller != null && controller.FrameCount > 1) controller.GotoFrame(0);
                }

                public void Restart()
                {
                    var controller = ImageBehavior.GetAnimationController(Control);
                    if (controller != null && controller.FrameCount > 1)
                    {
                        controller.GotoFrame(0);
                        controller.Play();
                    }
                }

            }

            private readonly IDictionary<Uri, Element> _elements = new Dictionary<Uri, Element>();

            public readonly Grid ImageContainer;

            public readonly bool Repeat;

            public VisualImageStimulusPresenter(Grid imageContainer, bool repeat)
            {
                ImageContainer = imageContainer;
                Repeat = repeat;
            }

            public bool Accept(MiStage.VisualStimulusType stimulusType) => stimulusType == MiStage.VisualStimulusType.Image;

            public IVisualElement Present(string content, bool preload)
            {
                var imageUri = new Uri(content);
                return _elements.GetOrCreate(imageUri, uri =>
                {
                    var image = new Image {Visibility = Visibility.Hidden};
                    ImageBehavior.SetAnimatedSource(image, new BitmapImage(uri));
                    ImageBehavior.SetAutoStart(image, true);
                    ImageBehavior.SetRepeatBehavior(image, Repeat ? RepeatBehavior.Forever : new RepeatBehavior(1));
                    ImageContainer.Children.Add(image);
                    return new Element {Control = image};
                });
            }

        }

        private class VisualVideoStimulusPresenter : IVisualStimulusPresenter
        {

            private class Element : IVisualElement
            {

                public Image Control;

                public DirectShowVideoSource Source;

                public void Show() => Control.Visibility = Visibility.Visible;

                public void Hide() => Control.Visibility = Visibility.Hidden;

                public void Play() => Source.Play();

                public void Pause() => Source.Pause();

                public void Rewind() => Source.Rewind();

                public void Restart() => Source.Reset(false);

            }

            private readonly IDictionary<Uri, Element> _elements = new Dictionary<Uri, Element>();

            public readonly Grid VideoContainer;

            public readonly bool Repeat;

            public VisualVideoStimulusPresenter(Grid videoContainer, bool repeat)
            {
                VideoContainer = videoContainer;
                Repeat = repeat;
            }

            public bool Accept(MiStage.VisualStimulusType stimulusType) => stimulusType == MiStage.VisualStimulusType.Video;

            public IVisualElement Present(string content, bool preload)
            {
                var videoUri = new Uri(content);
                return _elements.GetOrCreate(videoUri, uri =>
                {
                    var image = new Image { Visibility = Visibility.Hidden };
                    var source = new DirectShowVideoSource(uri, Repeat);
                    source.NewFrame += (s0, e0) => image.DispatcherInvoke(img => { img.Source = ((Bitmap)e0.Clone()).ToBitmapSource(); });
                    VideoContainer.Children.Add(image);
                    return new Element { Control = image, Source = source };
                });
            }

        }

        private class AuditoryTextStimulusPresenter : IAuditoryStimulusPresenter
        {

            private class Element : IAuditoryElement
            {

                public readonly SpeechSynthesizer Synthesizer = new SpeechSynthesizer();

                public string Text;

                public void Play() => Synthesizer.SpeakAsync(Text);

                public void Stop() { }

                public void Restart() { }

            }

            private readonly Element _textElement;

            public AuditoryTextStimulusPresenter() => _textElement = new Element();

            public bool Accept(MiStage.AuditoryStimulusType stimulusType) => stimulusType == MiStage.AuditoryStimulusType.Text;

            public IAuditoryElement Present(string content, bool repeat)
            {
                _textElement.Text = content;
                return _textElement;
            }

        }

        private class AuditoryAudioStimulusPresenter : IAuditoryStimulusPresenter
        {

            private class AudioElement : IAuditoryElement
            {

                public MediaElement Control;

                public void Play() => Control.Play();

                public void Stop()
                {
                    Control.Pause();
                    Control.Position = TimeSpan.Zero;
                }

                public void Restart()
                {
                    Control.Position = TimeSpan.Zero;
                    Play();
                }

            }

            private readonly IDictionary<Uri, AudioElement> _elements = new Dictionary<Uri, AudioElement>();

            public readonly Grid AudioContainer;

            public readonly bool Repeat;

            public AuditoryAudioStimulusPresenter(Grid audioContainer, bool repeat)
            {
                AudioContainer = audioContainer;
                Repeat = repeat;
            }

            public bool Accept(MiStage.AuditoryStimulusType stimulusType) => stimulusType == MiStage.AuditoryStimulusType.Audio;

            public IAuditoryElement Present(string content, bool preload)
            {
                var audioUri = new Uri(content);
                return _elements.GetOrCreate(audioUri, uri =>
                {
                    var audio = new MediaElement
                    {
                        Visibility = Visibility.Hidden,
                        LoadedBehavior = MediaState.Manual,
                        Source = uri
                    };
                    if (Repeat) audio.MediaEnded += (s0, e0) => ((MediaElement)s0).Position = TimeSpan.Zero;
                    AudioContainer.Children.Add(audio);
                    return new AudioElement { Control = audio };
                });
            }

        }

        [NotNull] private readonly Session _session;

        [NotNull] private readonly MiParadigm _paradigm;

        [CanBeNull] private readonly IMarkable _markable;

        [CanBeNull] private readonly MiStimClient _miStimClient;

        [CanBeNull] private readonly GazeFocusDetector _gazeFocusDetector;

        [NotNull] private readonly StageProgram _stageProgram;

        private readonly IList<IVisualStimulusPresenter> _visualStimulusPresenters;

        private readonly IList<IAuditoryStimulusPresenter> _auditoryStimulusPresenters;

        private IVisualElement _activeVisualElement;

        private IAuditoryElement _activeAuditoryElement;

        public MiExperimentWindow(Session session)
        {
            InitializeComponent();
            _session = session;
            _paradigm = (MiParadigm)session.Paradigm;

            _visualStimulusPresenters = new List<IVisualStimulusPresenter>
            {
                new VisualTextStimulusPresenter(CueTextBlock),
                new VisualImageStimulusPresenter(ImageContainer, _paradigm.Config.Experimental.Repeat),
                new VisualVideoStimulusPresenter(VideoContainer, _paradigm.Config.Experimental.Repeat)
            };
            _auditoryStimulusPresenters = new List<IAuditoryStimulusPresenter>
            {
                new AuditoryTextStimulusPresenter(),
                new AuditoryAudioStimulusPresenter(AudioContainer, _paradigm.Config.Experimental.Repeat)
            };
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();

            if (_paradigm.Config.Experimental.PreLoadResourceListFile != null)
            {
                foreach (var line in File.ReadAllLines(_paradigm.Config.Experimental.PreLoadResourceListFile))
                {
                    var verticalIndex = line.IndexOf("|", StringComparison.Ordinal);
                    if (verticalIndex <= 0) continue;
                    var type = line.Substring(0, verticalIndex);
                    var resource = line.Substring(verticalIndex + 1);
                    // ReSharper disable once SwitchStatementMissingSomeCases
                    switch (type.ToLowerInvariant())
                    {
                        case "visual":
                            PresentStimuli(MiStage.Stimulus<MiStage.VisualStimulusType>.Parse(resource), null, true);
                            break;
                        case "auditory":
                            PresentStimuli(null, MiStage.Stimulus<MiStage.AuditoryStimulusType>.Parse(resource), true);
                            break;
                    }
                }
            }

#if DEBUG
            if (!_paradigm.Config.Experimental.UseInternalProgram)
#else
            if (true)
#endif
                _miStimClient = new MiStimClient(session);

            if (_miStimClient != null)
            {
                // ReSharper disable once PossibleNullReferenceException
                _miStimClient.ProgressChanged += (sender, progress) => this.DispatcherInvoke(() => ProgressBar.Value = progress);
                _miStimClient.FocusRequested += (sender, e) => EnterRequestForFocusMode();
                _miStimClient.ControlCommandReceived += (sender, cmd) => OnControlCommandReceived(cmd);
            }

            /* Initialize GazeFocusDetector to enable 'request for focus' */
            if (session.StreamerCollection.TryFindFirst<GazePointStreamer>(out var gazePointStreamer))
            {
                _gazeFocusDetector = new GazeFocusDetector(session.Clock, (uint)TimeUnit.Millisecond.ConvertTo(_paradigm.Config.Experimental.GazeToFocusDuration, session.Clock.Unit));
                _gazeFocusDetector.Focused += (sender, e) => OnFocused();
                _gazeFocusDetector.Enter += (sender, e) => this.DispatcherInvoke(() => FocusCircle.Fill = Brushes.Pink);
                _gazeFocusDetector.Leave += (sender, e) => this.DispatcherInvoke(() => FocusCircle.Fill = Brushes.Red);
                gazePointStreamer.AttachConsumer(_gazeFocusDetector);
                if (gazePointStreamer.EyeTracker.GetType() != typeof(CursorTracker)) this.HideCursorInside();
            }
            else
                throw new UserException("'GazePointStreamer' is required for 'request for focus' feature.");

            Background = new SolidColorBrush(_paradigm.Config.Gui.BackgroundColor.ToSwmColor());
            CueTextBlock.Foreground = new SolidColorBrush(_paradigm.Config.Gui.FontColor.ToSwmColor());
            CueTextBlock.FontSize = _paradigm.Config.Gui.FontSize;
            ProgressBar.Background = new SolidColorBrush(_paradigm.Config.Gui.ProgressBarColor[ColorKeys.Background].ToSwmColor());
            ProgressBar.Foreground = new SolidColorBrush(_paradigm.Config.Gui.ProgressBarColor[ColorKeys.Foreground].ToSwmColor());
            ProgressBar.BorderThickness = new Thickness(_paradigm.Config.Gui.ProgressBarBorder.Width);
            ProgressBar.BorderBrush = new SolidColorBrush(_paradigm.Config.Gui.ProgressBarBorder.Color.ToSwmColor());

            _stageProgram = _paradigm.CreateStagedProgram(session, _miStimClient);
            _stageProgram.StageChanged += StageProgram_StageChanged;
        }

        public IntPtr Handle => new WindowInteropHelper(this).Handle;

        internal void UpdateTargetCircle() => _gazeFocusDetector?.SetTargetCircle(new Point(ActualWidth / 2, ActualHeight / 2), FocusCircle.Width / 2);

        internal void EnterRequestForFocusMode()
        {
            this.DispatcherInvoke(() =>
            {
                MainGrid.Visibility = Visibility.Hidden;
                FocusIndicationContainer.Visibility = Visibility.Visible;
                FocusCircle.Fill = Brushes.Red;
            });
            if (_gazeFocusDetector != null) _gazeFocusDetector.IsEnabled = true;
        }

        internal void OnFocused()
        {
            if (_gazeFocusDetector != null) _gazeFocusDetector.IsEnabled = false;
            _miStimClient?.SendFocusedSignal();
            this.DispatcherInvoke(() =>
            {
                MainGrid.Visibility = Visibility.Visible;
                FocusIndicationContainer.Visibility = Visibility.Hidden;
            });
        }

        internal void OnControlCommandReceived(MiStimClient.ControlCommand command)
        {
            this.DispatcherInvoke(() =>
            {
                switch (command)
                {
                    case MiStimClient.ControlCommand.Play:
                        _activeVisualElement.Play();
                        break;
                    case MiStimClient.ControlCommand.Pause:
                        _activeVisualElement.Pause();
                        break;
                    default:
                        Log.Warn("OnControlCommandReceived - unknown command", "command", command);
                        break;
                }
            });
        }

        private void PresentStimuli(MiStage.Stimulus<MiStage.VisualStimulusType> visualStimulus, 
            MiStage.Stimulus<MiStage.AuditoryStimulusType> auditoryStimulus, bool preload)
        {
            var forceReset = _paradigm.Config.Experimental.ForceReset;

            IVisualStimulusPresenter vsp = null;
            if (visualStimulus != null)
            {
                vsp = _visualStimulusPresenters.FirstOrDefault(p => p.Accept(visualStimulus.Type));
                if (vsp == null)
                    Log.Warn("PresentStimuli - visual stimulus presenter not found",
                        "type", visualStimulus.Type, "value", visualStimulus.Content);
            }
            var visualElement = vsp?.Present(visualStimulus.Content, preload);
            if (_activeVisualElement == visualElement)
            {
                if (visualElement != null && forceReset)
                    visualElement.Restart();
            }
            else
            {
                if (_activeVisualElement != null)
                {
                    _activeVisualElement.Rewind();
                    _activeVisualElement.Pause();
                    _activeVisualElement.Hide();
                }
                if (visualElement != null)
                {
                    visualElement.Show();
                    visualElement.Play();
                }
            }
            _activeVisualElement = visualElement;

            IAuditoryStimulusPresenter asp = null;
            if (auditoryStimulus != null)
            {
                asp = _auditoryStimulusPresenters.FirstOrDefault(p => p.Accept(auditoryStimulus.Type));
                if (asp == null)
                    Log.Warn("PresentStimuli - auditory stimulus presenter not found",
                        "type", auditoryStimulus.Type, "value", auditoryStimulus.Content);
            }
            var auditoryElement = asp?.Present(auditoryStimulus.Content, preload);
            if (_activeAuditoryElement == auditoryElement)
            {
                if (auditoryElement != null && forceReset)
                    auditoryElement.Restart();
            }
            else
            {
                _activeAuditoryElement?.Stop();
                auditoryElement?.Play();
            }
            _activeAuditoryElement = auditoryElement;

            if (preload) PresentStimuli(null, null, false);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _session.Start();
            _stageProgram.Start();
            _miStimClient?.SendStartSignal();
            UpdateTargetCircle();
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) => UpdateTargetCircle();

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (e.Key)
            {
                case Key.Escape:
                    _markable?.Mark(MarkerDefinitions.UserExitMarker);
                    Stop(true);
                    break;
                case Key.Up:
                    this.MoveToScreen((currentCenter, screenCenter) => screenCenter.Y > currentCenter.Y);
                    break;
                case Key.Down:
                    this.MoveToScreen((currentCenter, screenCenter) => screenCenter.Y < currentCenter.Y);
                    break;
                case Key.Left:
                    this.MoveToScreen((currentCenter, screenCenter) => screenCenter.X < currentCenter.X);
                    break;
                case Key.Right:
                    this.MoveToScreen((currentCenter, screenCenter) => screenCenter.X > currentCenter.X);
                    break;
            }
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        private void StageProgram_StageChanged(object sender, StageChangedEventArgs e)
        {
            /* Get next stage, exit on null (END REACHED) */
            if (e.IsEndReached)
            {
                this.DispatcherInvoke(() => Stop());
                return;
            }
            var stage = e.Stage;

            /* Record marker */
            if (stage.Marker != null) _markable?.Mark(stage.Marker.Value);

            var preload = false;
            var showProgressBar = false;
            MiStage.Stimulus<MiStage.VisualStimulusType> visualStimulus = null;
            MiStage.Stimulus<MiStage.AuditoryStimulusType> auditoryStimulus = null;

            if (stage is MiStage miStage)
            {
                preload = miStage.IsPreload;
                showProgressBar = miStage.ShowProgressBar;
                visualStimulus = miStage.VisualStimulus;
                auditoryStimulus = miStage.AuditoryStimulus;
            }

            Application.Current.Dispatcher.Invoke(() =>
            {
                ProgressBar.Visibility = !preload && showProgressBar ? Visibility.Visible : Visibility.Hidden;
                PresentStimuli(visualStimulus, auditoryStimulus, preload);
            });
        }

        private void Stop(bool userInterrupted = false)
        {
            Close();
            _miStimClient?.Close();
            _stageProgram.Stop();
            _session.Finish(null, userInterrupted);
        }

    }

}
