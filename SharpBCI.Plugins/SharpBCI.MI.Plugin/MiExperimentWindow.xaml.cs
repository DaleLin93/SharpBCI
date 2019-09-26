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
using System.Threading.Tasks;
using System.Windows.Interop;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.Logging;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Streamers;
using Application = System.Windows.Application;
using Image = System.Windows.Controls.Image;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using SharpBCI.Extensions.Devices.EyeTrackers;
using Brushes = System.Windows.Media.Brushes;
using Point = System.Windows.Point;

namespace SharpBCI.Paradigms.MI
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for MiExperimentWindow.xaml
    /// </summary>
    internal partial class MiExperimentWindow
    {

        private static readonly Logger Log = Logger.GetLogger(typeof(MiExperimentWindow));

        private class GazeFocusDetector : StreamConsumer<Timestamped<IGazePoint>>
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

            public event EventHandler Focused;

            public event EventHandler Enter;

            public event EventHandler Leave;

            private readonly LinkedList<GazeHitTest> _gazeHitTests = new LinkedList<GazeHitTest>();

            private bool _enabled = false;

            private bool _entered = false;

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
                if ( _entered != inside)
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

        private class ImageElement
        {

            public Image Control;

        }

        private class VideoElement
        {

            public Image Control;

            public DirectShowVideoSource Source;

        }

        private class AudioElement
        {

            public MediaElement Control;

        }

        private class Elements<T>
        {

            public readonly IDictionary<Uri, T> Resources = new Dictionary<Uri, T>();

            public T LastElement;

        }

        [NotNull] private readonly Session _session;

        [NotNull] private readonly MiParadigm _paradigm;

        [CanBeNull] private readonly IMarkable _markable;

        [CanBeNull] private readonly MiStimClient _miStimClient;

        [CanBeNull] private readonly GazeFocusDetector _gazeFocusDetector;

        [NotNull] private readonly StageProgram _stageProgram;

        private readonly SpeechSynthesizer _synthesizer = new SpeechSynthesizer();

        private readonly Elements<ImageElement> _imageElements = new Elements<ImageElement>();

        private readonly Elements<VideoElement> _videoElements = new Elements<VideoElement>();

        private readonly Elements<AudioElement> _audioElements = new Elements<AudioElement>();

        public MiExperimentWindow(Session session)
        {
            InitializeComponent();
            _session = session;
            _paradigm = (MiParadigm) session.Paradigm;
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();

            if (MiParadigm.MiStimClientProperty.TryGet(session, out _miStimClient))
            {
                // ReSharper disable once PossibleNullReferenceException
                _miStimClient.ProgressChanged += (sender, progress) => this.DispatcherInvoke(() => ProgressBar.Value = progress);
                _miStimClient.FocusRequested += (sender, e) => EnterRequestForFocusMode();
            }

            /* Initialize GazeFocusDetector to enable 'request for focus' */
            if (session.StreamerCollection.TryFindFirst<GazePointStreamer>(out var gazePointStreamer))
            {
                _gazeFocusDetector = new GazeFocusDetector(session.Clock, (uint)TimeUnit.Millisecond.ConvertTo(_paradigm.Config.Test.GazeToFocusDuration, session.Clock.Unit));
                _gazeFocusDetector.Focused += (sender, e) => OnFocused();
                _gazeFocusDetector.Enter += (sender, e) => this.DispatcherInvoke(() => FocusCircle.Fill = Brushes.Pink);
                _gazeFocusDetector.Leave += (sender, e) => this.DispatcherInvoke(() => FocusCircle.Fill = Brushes.Red);
                gazePointStreamer.Attach(_gazeFocusDetector);
                if (gazePointStreamer.EyeTracker.GetType() != typeof(CursorTracker)) this.HideCursorInside();
            }
            else
                throw new UserException("'GazePointStreamer' is required for 'request for focus' feature.");

            Background = new SolidColorBrush(_paradigm.Config.Gui.BackgroundColor.ToSwmColor());
            CueText.Foreground = new SolidColorBrush(_paradigm.Config.Gui.FontColor.ToSwmColor());
            CueText.FontSize = _paradigm.Config.Gui.FontSize;
            ProgressBar.Background = new SolidColorBrush(_paradigm.Config.Gui.ProgressBarColor[ColorKeys.Background].ToSwmColor());
            ProgressBar.Foreground = new SolidColorBrush(_paradigm.Config.Gui.ProgressBarColor[ColorKeys.Foreground].ToSwmColor());
            ProgressBar.BorderThickness = new Thickness(_paradigm.Config.Gui.ProgressBarBorder.Width);
            ProgressBar.BorderBrush = new SolidColorBrush(_paradigm.Config.Gui.ProgressBarBorder.Color.ToSwmColor());

            _stageProgram = _paradigm.CreateStagedProgram(session);
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
            var preload = false;

            /* Record marker */
            if (stage.Marker != null)
                _markable?.Mark(stage.Marker.Value);

            var showProgressBar = false;
            string displayTextContent = null;
            Uri videoUri = null;
            Uri imageUri = null;

            string synthesizerContent = null;
            Uri audioUri = null;

            if (stage is MiStage miStage)
            {
                showProgressBar = miStage.ShowProgressBar;
                preload = miStage.IsPreload;
                if (miStage.VisualStimulus != null)
                    switch (miStage.VisualStimulus.Type)
                    {
                        case MiStage.VisualStimulusType.Text:
                            displayTextContent = miStage.VisualStimulus.Content;
                            break;
                        case MiStage.VisualStimulusType.Video:
                            videoUri = new Uri(miStage.VisualStimulus.Content);
                            break;
                        case MiStage.VisualStimulusType.Image:
                            imageUri = new Uri(miStage.VisualStimulus.Content);
                            break;
                        default:
                            throw new ArgumentException();
                    }
                if (miStage.AuditoryStimulus != null)
                    switch (miStage.AuditoryStimulus.Type)
                    {
                        case MiStage.AuditoryStimulusType.Text:
                            synthesizerContent = miStage.AuditoryStimulus.Content;
                            break;
                        case MiStage.AuditoryStimulusType.Audio:
                            audioUri = new Uri(miStage.AuditoryStimulus.Content);
                            break;
                        default:
                            throw new ArgumentException();
                    }
            }

            var repeat = _paradigm.Config.Test.Repeat;
            var forceReset = _paradigm.Config.Test.ForceReset;

            lock (this)
                Application.Current.Dispatcher.Invoke(() =>
                {
                    ProgressBar.Visibility = showProgressBar ? Visibility.Visible : Visibility.Hidden;

                    CueText.Visibility = displayTextContent == null || preload ? Visibility.Hidden : Visibility.Visible;
                    CueText.Text = displayTextContent ?? "";

                    if (!preload && synthesizerContent != null) _synthesizer.SpeakAsync(synthesizerContent);

                    ImageElement imageElement = null;
                    if (imageUri != null)
                        imageElement = _imageElements.Resources.GetOrCreate(imageUri, uri =>
                        {
                            var image = new Image { Visibility = Visibility.Hidden };
                            ImageBehavior.SetAutoStart(image, true);
                            ImageBehavior.SetRepeatBehavior(image, repeat ? RepeatBehavior.Forever : new RepeatBehavior(1));
                            ImageBehavior.SetAnimatedSource(image, new BitmapImage(uri));
                            ImageContainer.Children.Add(image);
                            return new ImageElement { Control = image };
                        }, preload
                            ? (Action<ImageElement>)null
                            : element =>
                            {
                                var controller = ImageBehavior.GetAnimationController(element.Control);
                                var animating = controller != null && controller.FrameCount > 1;
                                if (animating)
                                    controller.GotoFrame(0);
                                element.Control.Visibility = Visibility.Visible;
                                if (animating)
                                    controller.Play();
                            });
                    if (_imageElements.LastElement != null)
                    {
                        var controller = ImageBehavior.GetAnimationController(_imageElements.LastElement.Control);
                        if (_imageElements.LastElement == imageElement)
                        {
                            if (forceReset) controller?.GotoFrame(0);
                        }
                        else
                        {
                            _imageElements.LastElement.Control.Visibility = Visibility.Hidden;
                            controller?.Pause();
                            controller?.GotoFrame(0);
                        }
                    }

                    VideoElement videoElement = null;
                    if (videoUri != null)
                        videoElement = _videoElements.Resources.GetOrCreate(videoUri, uri =>
                        {
                            var image = new Image { Visibility = Visibility.Hidden };
                            var source = new DirectShowVideoSource(uri, repeat);
                            source.NewFrame += (s0, e0) => image.DispatcherInvoke(img => { img.Source = ((Bitmap)e0.Clone()).ToBitmapSource(); });
                            VideoContainer.Children.Add(image);
                            return new VideoElement { Control = image, Source = source };
                        }, element =>
                        {
                            Log.Info("StageProgram_StageChanged - init", "uri", element.Source.Uri);
                            if (!preload) element.Control.Visibility = Visibility.Visible;
                            element.Source.Play();
                        });
                    if (_videoElements.LastElement != null)
                    {
                        var readonlyLastVideoElement = _videoElements.LastElement;
                        if (_videoElements.LastElement == videoElement)
                        {
                            if (forceReset)
                            {
                                Log.Info("StageProgram_StageChanged - video force reset", "uri", videoUri);
                                Task.Run(() => readonlyLastVideoElement.Source.Rewind());
                            }
                        }
                        else
                        {
                            Log.Info("StageProgram_StageChanged - rewind", "uri", readonlyLastVideoElement.Source.Uri);
                            _videoElements.LastElement.Control.Visibility = Visibility.Hidden;
                            Task.Run(() =>
                            {
                                readonlyLastVideoElement.Source.Pause();
                                readonlyLastVideoElement.Source.Rewind();
                            });
                        }
                    }

                    AudioElement audioElement = null;
                    if (audioUri != null)
                        audioElement = _audioElements.Resources.GetOrCreate(audioUri, uri =>
                        {
                            var audio = new MediaElement
                            {
                                Visibility = Visibility.Hidden,
                                LoadedBehavior = MediaState.Manual,
                                Source = uri
                            };
                            if (repeat) audio.MediaEnded += (s0, e0) => ((MediaElement)s0).Position = TimeSpan.Zero;
                            AudioContainer.Children.Add(audio);
                            return new AudioElement { Control = audio };
                        }, preload ? (Action<AudioElement>)null : element => element.Control.Play());
                    if (_audioElements.LastElement != null)
                    {
                        if (_audioElements.LastElement == audioElement)
                        {
                            if (forceReset) _audioElements.LastElement.Control.Position = TimeSpan.Zero;
                        }
                        else
                            _audioElements.LastElement.Control.Stop();
                    }

                    _imageElements.LastElement = imageElement;
                    _videoElements.LastElement = videoElement;
                    _audioElements.LastElement = audioElement;
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
