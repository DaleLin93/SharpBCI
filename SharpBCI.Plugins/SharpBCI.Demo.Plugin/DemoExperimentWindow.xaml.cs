using SharpBCI.Extensions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;

namespace SharpBCI.Paradigms.Demo
{
    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for DemoExperimentWindow.xaml
    /// </summary>
    internal partial class DemoExperimentWindow
    {

        /// <summary>
        /// Current running session.
        /// </summary>
        private readonly Session _session;

        /// <summary>
        /// Markable interface to record markers during the paradigm.
        /// </summary>
        private readonly IMarkable _markable;

        public DemoExperimentWindow(Session session, DemoParadigm paradigm)
        {
            InitializeComponent();

            _session = session;
            _markable = session.StreamerCollection.FindFirstOrDefault<IMarkable>();

            /* Set paradigm parameters to this window. */
            CueTextBlock.Text = paradigm.Text;
            CueTextBlock.FontSize = paradigm.FontSize;
            CueTextBlock.Foreground = new SolidColorBrush(paradigm.BackgroundColor.ToSwmColor());
            Background = new SolidColorBrush(paradigm.BackgroundColor.ToSwmColor());
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
                case Key.Escape: /* Quit this paradigm */
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
