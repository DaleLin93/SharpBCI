using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Paradigms.Profiler
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for ProfilerWindow.xaml
    /// </summary>
    internal partial class ProfilerWindow
    {

        private readonly Session _session;

        private class Watcher : IFilter, IConsumer
        {

            public ulong InputCount, ProcessCount;

            public Watcher() => Reset();

            public Type AcceptType => typeof(object);

            public Priority Priority => Priority.Monitor;

            public void Reset()
            {
                InputCount = 0;
                ProcessCount = 0;
            }

            bool IFilter.Accept(object value)
            {
                InputCount++;
                return true;
            }

            void IConsumer.Accept(object value) => ProcessCount++;

        }

        private class WatcherDataViewModel : IDisposable
        {

            public readonly IStreamer Streamer;

            public readonly TextBlock CountTextBlock, InputSpeedTextBlock, ProcessingSpeedTextBlock;

            public readonly Watcher Watcher;

            public WatcherDataViewModel(IStreamer streamer, TextBlock inputSpeedTextBlock, TextBlock processingSpeedTextBlock, TextBlock countTextBlock)
            {
                Streamer = streamer;
                InputSpeedTextBlock = inputSpeedTextBlock;
                ProcessingSpeedTextBlock = processingSpeedTextBlock;
                CountTextBlock = countTextBlock;
                Watcher = new Watcher();
            }

            ~WatcherDataViewModel()
            {
                Dispose(false);
            }

            public void AttachWatcher()
            {
                Streamer.AttachFilter(Watcher);
                Streamer.AttachConsumer(Watcher);
            }

            public void DetachWatcher()
            {
                Streamer.AttachFilter(Watcher);
                Streamer.AttachConsumer(Watcher);
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (disposing) DetachWatcher();
            }

        }

        private Timer _timer;

        private WatcherDataViewModel[] _profileViewModels;

        private long _startTimestamp;

        public ProfilerWindow(Session session)
        {
            InitializeComponent();
            _session = session;
        }

        private static Type GetValueType(IStreamer streamer)
        {
            var valueType = streamer.ValueType;
            if (valueType.IsGenericType
                && !valueType.IsGenericTypeDefinition
                && valueType.GetGenericTypeDefinition() == typeof(Timestamped<>))
                valueType = valueType.GetGenericType(typeof(Timestamped<>));
            return valueType;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _session.Start();
            var streamers = _session.StreamerCollection.Values;
            var profileViewModels = new WatcherDataViewModel[streamers.Length];
            for (var i = 0; i < streamers.Length; i++)
            {
                var streamer = streamers[i];
                var valueType = GetValueType(streamers[i]);
                var stackPanel = StackPanel.AddGroupStackPanel($"{streamer.GetType().Name}<{valueType.Name}>", null);
                var inputSpeedTextBlock = new TextBlock {Text = "0/s"};
                var processingSpeedTextBlock = new TextBlock {Text = "0/s"};
                var totalCountTextBlock = new TextBlock {Text = "0/0"};
                stackPanel.AddLabeledRow("Input Speed", inputSpeedTextBlock);
                stackPanel.AddLabeledRow("Processing Speed", processingSpeedTextBlock);
                stackPanel.AddLabeledRow("Processed/Input Count", totalCountTextBlock);
                profileViewModels[i] = new WatcherDataViewModel(streamer, inputSpeedTextBlock, processingSpeedTextBlock, totalCountTextBlock);
            }
            _startTimestamp = DateTimeUtils.CurrentTimeMillis;
            foreach (var profileViewModel in profileViewModels)
                profileViewModel.AttachWatcher();
            _profileViewModels = profileViewModels;
            _timer = new Timer(Timer_OnTick, null, 1000, 1000);
        }

        private void Timer_OnTick(object state)
        {
            this.DispatcherInvoke(() =>
            {
                var secs = (DateTimeUtils.CurrentTimeMillis - _startTimestamp) / 1000.0;
                foreach (var profileViewModel in _profileViewModels)
                {
                    profileViewModel.InputSpeedTextBlock.Text = $"{profileViewModel.Watcher.InputCount / secs:N2}/s";
                    profileViewModel.ProcessingSpeedTextBlock.Text = $"{profileViewModel.Watcher.ProcessCount / secs:N2}/s";
                    profileViewModel.CountTextBlock.Text = $"{profileViewModel.Watcher.ProcessCount}/{profileViewModel.Watcher.InputCount}";
                }
            });
        }

        private void Stop(bool userInterrupted = false)
        {
            Close();
            _session.Finish(null, userInterrupted);
            _timer?.Dispose();
            _timer = null;
        }

        private void ResetButton_OnClick(object sender, RoutedEventArgs e)
        {
            _startTimestamp = DateTimeUtils.CurrentTimeMillis;
            foreach (var profileViewModel in _profileViewModels)
                profileViewModel.Watcher.Reset();
        }

        private void StopButton_OnClick(object sender, RoutedEventArgs e) => Stop(true);

    }

}
