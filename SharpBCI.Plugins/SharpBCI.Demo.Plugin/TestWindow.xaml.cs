using SharpBCI.Core.Experiment;
using SharpBCI.Extensions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MarukoLib.UI;
using SharpBCI.Core.IO;

namespace SharpBCI.Experiments.Demo
{
    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for TestWindow.xaml
    /// </summary>
    internal partial class TestWindow
    {

        /// <summary>
        /// Current running session.
        /// </summary>
        private readonly Session _session;

        /// <summary>
        /// Markable interface to record markers during the experiment.
        /// </summary>
        private readonly IMarkable _markable;

        public TestWindow(Session session, DemoExperiment experiment)
        {
            InitializeComponent();

            _session = session;
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();

            /* Set experiment parameters to this window. */
            CueTextBlock.Text = experiment.Text;
            CueTextBlock.FontSize = experiment.FontSize;
            CueTextBlock.Foreground = new SolidColorBrush(experiment.BackgroundColor.ToSwmColor());
            Background = new SolidColorBrush(experiment.BackgroundColor.ToSwmColor());
        }

        /// <summary>
        /// Handling Loaded event 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_OnLoaded(object sender, RoutedEventArgs e) => _session.Start();

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Escape: /* Quit this experiment */
                    _markable?.Mark(MarkerDefinitions.UserExitMarker);
                    Stop(true);
                    break;
            }
        }

        private void Stop(bool userInterrupted = false)
        {
            Close();
            _session.Finish(userInterrupted);
        }

    }
}
