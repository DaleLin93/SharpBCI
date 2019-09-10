using SharpBCI.Core.Experiment;
using SharpBCI.Core.Staging;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MarukoLib.UI;
using SharpBCI.Core.IO;
using SharpBCI.Extensions;

namespace SharpBCI.Experiments.CPT
{
    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for TestWindow.xaml
    /// </summary>
    internal partial class TestWindow
    {

        public class ActivedStage
        {

            public readonly ulong Timestamp;

            public readonly CptStage Stage;

            public readonly CptExperiment.CptTrial Trial;

            public volatile bool Pressed;

            public ActivedStage(ulong timestamp, CptStage stage, CptExperiment.CptTrial trial)
            {
                Timestamp = timestamp;
                Stage = stage;
                Trial = trial;
            }

        }

        private readonly Session _session;

        private readonly IMarkable _markable;

        private readonly CptExperiment.Result _result;

        private readonly StageProgram _stageProgram;

        private readonly LinkedList<CptExperiment.CptTrial> _trials = new LinkedList<CptExperiment.CptTrial>();

        private volatile ActivedStage _currentStage;

        public TestWindow(Session session)
        {
            InitializeComponent();

            _session = session;
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();

            this.HideCursorInside();

            var experiment = (CptExperiment)session.Experiment;

            Background = new SolidColorBrush(experiment.Config.Gui.BackgroundColor.ToSwmColor());
            CueTextBlock.Foreground = new SolidColorBrush(experiment.Config.Gui.FontColor.ToSwmColor());
            CueTextBlock.FontSize = experiment.Config.Gui.FontSize;

            _stageProgram = experiment.CreateStagedProgram(session);
            _stageProgram.StageChanged += StageProgram_StageChanged;
            _result = new CptExperiment.Result { Trials = _trials };
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _session.Start();
            _stageProgram.Start();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            var now = CurrentTime;
            var activedStage = _currentStage;
            if (e.Key != Key.Space || (activedStage?.Pressed ?? true)) return;
            activedStage.Pressed = true;
            _markable?.Mark(CptExperiment.UserActionMarker);
            activedStage.Trial.Reply(now);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
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
            if (stage.Marker != null)
                _markable?.Mark(stage.Marker.Value);

            this.DispatcherInvoke(() => 
            {
                /* Update text */
                CueTextBlock.Text = stage.Cue;

                /* Update stage */
                if (stage is CptStage cptStage)
                {
                    var now = CurrentTime;
                    var trial = new CptExperiment.CptTrial
                    {
                        Target = cptStage.IsTarget,
                        Timestamp = now,
                        Replied = false,
                        ReactionTime = -1
                    };
                    _currentStage = new ActivedStage(now, cptStage, trial);
                    _trials.AddLast(trial);
                }
                else
                    _currentStage = null;

                /* Set focus */
                if (!IsFocused) Focus();
            });
        }

        private void Stop(bool userInterrupted = false)
        {
            Close();
            _stageProgram.Stop();
            _result.Duration = _stageProgram.ProgramTime;
            _session.Finish(_result, userInterrupted);
        }

        private ulong CurrentTime => _session.SessionTime;

    }
}
