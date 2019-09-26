using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Streamers;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace SharpBCI.Extensions.Devices.MarkerSources
{


    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for MarkerDisplayWindow.xaml
    /// </summary>
    internal partial class MarkerDisplayWindow : IStreamConsumer<Timestamped<IMarker>>
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
            _streamer.Attach(this);

            _maxRecordCount = maxRecordCount;
        }

        private void Window_OnLoaded(object sender, RoutedEventArgs e) => _streamer.Start();

        private void Window_OnClosed(object sender, EventArgs e) => _streamer.Stop();

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
        }

        Type IStreamConsumer.AcceptType => typeof(Timestamped<IMarker>);

        StreamConsumerPriority IStreamConsumer.Priority => StreamConsumerPriority.Lowest;

        void IStreamConsumer.Accept(object value) => ((IStreamConsumer<Timestamped<IMarker>>) this).Accept((Timestamped<IMarker>) value);

        void IStreamConsumer<Timestamped<IMarker>>.Accept(Timestamped<IMarker> value)
        {
            this.DispatcherInvoke(() =>
            {
                _marks.Add(value);
                while (_marks.Count > _maxRecordCount) _marks.RemoveAt(0);
            });
        }

    }
}
