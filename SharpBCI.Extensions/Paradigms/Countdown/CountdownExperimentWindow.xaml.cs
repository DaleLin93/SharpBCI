using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.Paradigms.Countdown
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for CountdownExperimentWindow.xaml
    /// </summary>
    internal partial class CountdownExperimentWindow
    {

        private readonly Session _session;

        private readonly IMarkable _markable;

        private readonly StageProgram _stageProgram;

        public CountdownExperimentWindow(Session session)
        {
            InitializeComponent();

            _session = session;
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();
            this.HideCursorInside();

            var paradigm = (CountdownParadigm) session.Paradigm;
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
