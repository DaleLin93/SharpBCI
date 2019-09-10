using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.Experiments.Rest
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for TestWindow.xaml
    /// </summary>
    internal partial class TestWindow
    {

        private readonly Session _session;

        private readonly StageProgram _stageProgram;

        public TestWindow(Session session)
        {
            InitializeComponent();

            _session = session;
            this.HideCursorInside();

            var experiment = (RestExperiment) session.Experiment;
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
            if (e.IsEndReached)
            {
                this.DispatcherInvoke(() => Stop());
                return;
            }
            this.DispatcherInvoke(() => CueText.Text = e.Stage.Cue);
        }

        private void Stop(bool userInterrupted = false)
        {
            Close();
            _stageProgram.Stop();
            _session.Finish(null, userInterrupted);
        }

    }

}
