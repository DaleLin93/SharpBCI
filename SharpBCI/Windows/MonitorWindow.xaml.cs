using System;
using System.Windows;
using SharpBCI.Core.IO;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Extensions.Devices;
using SharpBCI.Extensions.Streamers;

namespace SharpBCI.Windows
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for ResultWindow.xaml
    /// TODO: Rewrite with full D2D window.
    /// </summary>
    public partial class MonitorWindow
    {

        private class MonitorGazePointConsumer : Consumer<Timestamped<IGazePoint>>
        {

            public Action<Point> Callback;

            public override void Accept(Timestamped<IGazePoint> data) => Callback?.Invoke(new Point(data.Value.X, data.Value.Y));

        }

        private class MonitorSampleConsumer : Consumer<Timestamped<ISample>>
        {

            public Action<double[]> Callback;

            public override void Accept(Timestamped<ISample> data) => Callback?.Invoke(data.Value.Values);

        }

        private class ChannelSelection
        {
            public int Index;

            public override string ToString() => "Channel " + (Index + 1);
        }

        private static readonly WeakReference<MonitorWindow> Instance = new WeakReference<MonitorWindow>(null);
        
        private MonitorGazePointConsumer _monitorGazePointConsumer;

        //private MonitorSampleConsumer _monitorSampleConsumer;

        private MonitorWindow()
        {
            InitializeComponent();
        }

        public static bool IsShown => Instance.TryGetTarget(out var window) && window.IsVisible;

        public new static MonitorWindow Show()
        {
            if (!Instance.TryGetTarget(out var window))
                Instance.SetTarget(window = new MonitorWindow());
            ((Window)window).Show();
            return window;
        }

        public new static void Close()
        {
            if (Instance.TryGetTarget(out var window))
                ((Window) window).Close();
        }

        public void Bind(StreamerCollection streamerCollection)
        {
            Release();

            if (streamerCollection.TryFindFirst<GazePointStreamer>(out var gazeStream))
            {
                _monitorGazePointConsumer = new MonitorGazePointConsumer
                { Callback = point => this.DispatcherInvoke(() => GazePoint.Margin = new Thickness(point.X / 10, point.Y / 10, 0, 0)) };
                gazeStream.Attach(_monitorGazePointConsumer);
            }

        }

        public void Release()
        {
            _monitorGazePointConsumer.Callback = null;
            //_monitorSampleConsumer.Callback = null;
            ChannelComboBox.ItemsSource = null;
        }

        private void UpdateChannelSelection(uint channelNum)
        {
            var selections = new ChannelSelection[channelNum];
            for (int i = 0; i < channelNum; i++)
                selections[i] = new ChannelSelection { Index = i };
            ChannelComboBox.ItemsSource = selections;
            ChannelComboBox.SelectedIndex = 0;
        }

        private void MonitorWindow_OnClosed(object sender, EventArgs e)
        {
            Release();
        }

    }

}
