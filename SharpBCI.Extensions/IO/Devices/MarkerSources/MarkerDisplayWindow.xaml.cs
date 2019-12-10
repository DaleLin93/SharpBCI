using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Core.IO;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace SharpBCI.Extensions.IO.Devices.MarkerSources
{


    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for MarkerDisplayWindow.xaml
    /// </summary>
    internal partial class MarkerDisplayWindow : IConsumer<Timestamped<IMarker>>
    {

        private readonly ObservableCollection<Timestamped<IMarker>> _marks = new ObservableCollection<Timestamped<IMarker>>();

        private readonly MarkerStreamer _streamer;

        private readonly int _maxRecordCount;

        public MarkerDisplayWindow(MarkerStreamer streamer, int maxRecordCount)
        {
            InitializeComponent();
            MarkListView.ItemsSource = _marks;

            Loaded += Window_OnLoaded;
            Closed += Window_OnClosed;
            KeyUp += Window_OnKeyUp;

            _streamer = streamer;
            _streamer.AttachConsumer(this);

            _maxRecordCount = maxRecordCount;
        }

        private void Window_OnLoaded(object sender, RoutedEventArgs e) => _streamer.Start();

        private void Window_OnClosed(object sender, EventArgs e) => _streamer.Stop();

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        Type IComponent.AcceptType => typeof(Timestamped<IMarker>);

        Priority IPriority.Priority => Priority.Lowest;

        void IConsumer.Accept(object value) => ((IConsumer<Timestamped<IMarker>>) this).Accept((Timestamped<IMarker>) value);

        void IConsumer<Timestamped<IMarker>>.Accept(Timestamped<IMarker> value)
        {
            this.DispatcherInvoke(() =>
            {
                _marks.Add(value);
                while (_marks.Count > _maxRecordCount) _marks.RemoveAt(0);
            });
        }

    }
}
