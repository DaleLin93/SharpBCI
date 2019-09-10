using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.Experiments.Countdown
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for TestWindow.xaml
    /// </summary>
    internal partial class TestWindow
    {

        private readonly Session _session;

        private readonly IMarkable _markable;

        private readonly StageProgram _stageProgram;

        public TestWindow(Session session)
        {
            InitializeComponent();

            _session = session;
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();
            this.HideCursorInside();

            var experiment = (CountdownExperiment) session.Experiment;
            Background = new SolidColorBrush(experiment.Config.Gui.BackgroundColor.ToSwmColor());
            CueText.Foreground = new SolidColorBrush(experiment.Config.Gui.ForegroundColor.ToSwmColor());
            CueText.FontSize = experiment.Config.Gui.FontSize;

            _stageProgram = experiment.CreateStagedProgram(session);
            _stageProgram.StageChanged += StageProgram_StageChanged;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _session.Start();
            _stageProgram.Start();
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape:
                    Stop(true);
                    break;
            }
        }

        private void StageProgram_StageChanged(object sender, StageChangedEventArgs e)
        {
            /* Get next stage, exit on null (END REACHED) */
            if (!e.TryGetStage(out var stage))
            {
                this.DispatcherInvoke(() => Stop());
                return;
            }
            if (stage.Marker != null) _markable?.Mark(stage.Marker.Value);
            this.DispatcherInvoke(() => CueText.Text = stage.Cue);
        }

        private void Stop(bool userInterrupted = false)
        {
            Close();
            _stageProgram.Stop();
            _session.Finish(null, userInterrupted);
        }

    }

}
