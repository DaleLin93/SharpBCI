using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.Paradigms.Rest
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for RestExperimentWindow.xaml
    /// </summary>
    internal partial class RestExperimentWindow
    {

        private readonly Session _session;

        private readonly StageProgram _stageProgram;

        public RestExperimentWindow(Session session)
        {
            InitializeComponent();

            _session = session;
            this.HideCursorInside();

            var paradigm = (RestParadigm) session.Paradigm;
            Background = new SolidColorBrush(paradigm.Config.Gui.BackgroundColor.ToSwmColor());
            CueText.Foreground = new SolidColorBrush(paradigm.Config.Gui.ForegroundColor.ToSwmColor());
            CueText.FontSize = paradigm.Config.Gui.FontSize;

            _stageProgram = paradigm.CreateStagedProgram(session);
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
