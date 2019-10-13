using System;
using System.Collections.Generic;
using System.Windows;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.UI;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Streamers;
using SharpBCI.Plugins;

namespace SharpBCI.Windows
{

    internal class VisualElement : FrameworkElement
    {

        // Create a collection of child visual objects.
        public readonly VisualCollection Children;

        public VisualElement() => Children = new VisualCollection(this);

        // Provide a required override for the VisualChildrenCount property.
        protected override int VisualChildrenCount => Children.Count;

        // Provide a required override for the GetVisualChild method.
        protected override Visual GetVisualChild(int index) => Children[index];

    }

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for AnalysisWindow.xaml
    /// </summary>
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    partial class AnalysisWindow
    {

        internal class MarkerRecordItem
        {

            public int Marker { get; set; }

            public string Definition { get; set; }

            public ulong Timestamp { get; set; }

            public ulong DeltaT { get; set; }

        }

        private static readonly Pen BiosignalPen = new Pen(Brushes.Black, 1);

        private readonly string _dataFilePrefix;

        private readonly IList<BiosignalRecord> _biosignalRecords;

        private readonly IList<GazePointRecord> _gazePointRecords;

        private readonly IList<MarkerRecord> _markerRecords;

        private const double BiosignalFrequency = 500;

        private int _biosignalWindowSize = 500;

        private int _biosignalPage = 0;

        public AnalysisWindow(string dataFilePrefix)
        {
            InitializeComponent();

            Title = $"Marker Analysis: {dataFilePrefix}";

            _dataFilePrefix = dataFilePrefix;

            _biosignalRecords = LoadBiosignalRecords(dataFilePrefix, BiosignalFrequency);
            _gazePointRecords = LoadGazePointRecords(dataFilePrefix);
            _markerRecords = LoadMarkerRecords(dataFilePrefix);

            var moduleComboBoxItems = new LinkedList<object>();
            foreach (var plugin in App.Instance.Registries.Registry<Plugin>().Registered.Where(p => p.CustomMarkers.Count > 0))
            {
                var moduleItemContainer = new Grid();
                moduleItemContainer.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
                moduleItemContainer.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
                var nameTextBlock = new TextBlock
                {
                    Margin = new Thickness {Left = 5},
                    Text = plugin.Name,
                    Foreground = Brushes.Black,
                    FontWeight = FontWeights.Bold
                };
                var codeTextBlock = new TextBlock
                {
                    Margin = new Thickness {Left = 8},
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = $"({plugin.CustomMarkers.Count})",
                    Foreground = Brushes.SlateGray,
                    FontWeight = FontWeights.Light,
                    FontSize = 8
                };
                moduleItemContainer.Children.Add(nameTextBlock);
                moduleItemContainer.Children.Add(codeTextBlock);
                Grid.SetColumn(nameTextBlock, 0);
                Grid.SetColumn(codeTextBlock, 1);
                moduleComboBoxItems.AddLast(moduleItemContainer);
            }
            ModulePluginComboBox.ItemsSource = moduleComboBoxItems;
            if (ModulePluginComboBox.ItemsSource.Count() > 0) ModulePluginComboBox.SelectedIndex = 0;
        }

        private static IList<BiosignalRecord> LoadBiosignalRecords(string dataFilePrefix, double? frequency)
        {
            var file = dataFilePrefix + BiosignalAsciiFileWriter.FileSuffix;
            if (!File.Exists(file)) return EmptyArray<BiosignalRecord>.Instance;
            var sampleRecords = new LinkedList<BiosignalRecord>();
            foreach (var line in File.ReadLines(file, Encoding.UTF8))
                if (BiosignalRecord.TryParse(line, 0, out var record))
                    sampleRecords.AddLast(record);
            return BiosignalRecord.SmoothTimestamp(sampleRecords, 1000 / frequency);
        }

        private static IList<GazePointRecord> LoadGazePointRecords(string dataFilePrefix)
        {
            var file = dataFilePrefix + GazePointAsciiFileWriter.FileSuffix;
            if (!File.Exists(file)) return EmptyArray<GazePointRecord>.Instance;
            var gazePointRecords = new LinkedList<GazePointRecord>();
            foreach (var line in File.ReadLines(file, Encoding.UTF8))
                if (GazePointRecord.TryParse(line, 0, out var record))
                    gazePointRecords.AddLast(record);
            return gazePointRecords.OrderByTimestamp().ToList();
        }

        private static IList<MarkerRecord> LoadMarkerRecords(string dataFilePrefix)
        {
            var file = dataFilePrefix + MarkerAsciiFileWriter.FileSuffix;
            if (!File.Exists(file)) return EmptyArray<MarkerRecord>.Instance;
            var markerRecords = new LinkedList<MarkerRecord>();
            foreach (var line in File.ReadLines(file, Encoding.UTF8))
                if (MarkerRecord.TryParse(line, 0, out var record))
                    markerRecords.AddLast(record);
            return markerRecords.OrderByTimestamp().ToList();
        }

        private uint PageCount
        {
            get
            {
                var sampleCount = _biosignalRecords?.Count ?? 0;
                var pageCount = sampleCount / _biosignalWindowSize;
                if (sampleCount % _biosignalWindowSize > 0) pageCount++;
                return (uint)pageCount;
            }
        }

        private readonly object _lock = new object();
        private CancellationTokenSource _tokenSource = null;

        private void DelayedUpdateSignals()
        {
            lock (_lock)
            {
                _tokenSource?.Cancel();
                var tokenSource = _tokenSource = new CancellationTokenSource();
                Task.Delay(500, tokenSource.Token).ContinueWith(t =>
                {
                    if (!t.IsCompleted) return;
                    this.DispatcherInvoke(UpdateSignals);
                    lock (_lock) if (_tokenSource == tokenSource) _tokenSource = null;
                }, tokenSource.Token);
            }
        }

        private void UpdateSignals()
        {
            _biosignalWindowSize = Math.Max(100, Math.Min(5000, _biosignalWindowSize));
            WindowSizeTextBlock.Text = _biosignalWindowSize.ToString();

            _biosignalPage = Math.Max(0, Math.Min((int)PageCount - 1, _biosignalPage));
            PageTextBlock.Text = $"{_biosignalPage + 1}/{PageCount}";

            var windowLength = _biosignalWindowSize;
            var sampleOffset = _biosignalWindowSize * _biosignalPage;
            var sampleCount = Math.Min(_biosignalRecords.Count - sampleOffset, windowLength);
            var channelNum = _biosignalRecords.Count <= 0 ? 0 : _biosignalRecords[0].Values.Length;
            var samples = new double[sampleCount, channelNum];
            for (var s = 0; s < sampleCount; s++)
            {
                var channels = _biosignalRecords[sampleOffset + s].Values;
                for (var ch = 0; ch < channelNum; ch++)
                    samples[s, ch] = channels[ch];
            }

            double range;
            double[] chMeans;
            {
                var channelSums = new double[channelNum];
                var max = double.NegativeInfinity;
                var min = double.PositiveInfinity;
                for (var s = 0; s < sampleCount; s++)
                    for (var ch = 0; ch < channelNum; ch++)
                    {
                        var channel = samples[s, ch];
                        channelSums[ch] += channel;
                        if (max < channel)
                            max = channel;
                        if (min > channel)
                            min = channel;
                    }

                range = max - (min + max) / 2;
                for (var i = 0; i < channelSums.Length; i++)
                    channelSums[i] /= sampleCount;
                chMeans = channelSums;
            }

            var clientSize = new Size(BiosignalCanvas.ActualWidth, BiosignalCanvas.ActualHeight);
            var channelHeight = clientSize.Height / channelNum;
            var sampleInterval = clientSize.Width / windowLength;

            var segmentCount = (int) Math.Floor(clientSize.Width / this.GetVisualScaling() / 5);
            var stepLength = clientSize.Width / segmentCount;

            if (stepLength < sampleInterval)
            {
                segmentCount = sampleCount - 1;
                stepLength = sampleInterval;
            }
            
            Point Point(double xOffset, double yOffset, int sIdx, int chIdx) =>
                new Point(xOffset, yOffset + channelHeight / 2 + (samples[sIdx, chIdx] - chMeans[chIdx]) / range * channelHeight / 2 * 0.9);

            Point Interpolate(double xOffset, double yOffset, double sampleAt, int chIdx)
            {
                var y0Idx = Math.Max((int)Math.Floor(sampleAt), 0);
                var y1Idx = Math.Min((int) Math.Ceiling(sampleAt), sampleCount - 1);
                var y0 = samples[y0Idx, chIdx] - chMeans[chIdx];
                var y1 = samples[y1Idx, chIdx] - chMeans[chIdx];
                var alpha = sampleAt - y0Idx;
                var y = y0 + (y1 - y0) * alpha;
                return new Point(xOffset, yOffset + channelHeight / 2 + y / range * channelHeight / 2 * 0.9);
            }

            BiosignalVisualElement.Children.Clear();
            for (var ch = 0; ch < channelNum; ch++)
            {
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    var yOffset = ch * channelHeight;
                    var prev = Point(0, yOffset, 0, ch);
                    for (var i = 1; i < segmentCount; i++)
                    {
                        var sampleAt = i * stepLength / clientSize.Width * windowLength;
                        var current = Interpolate(i * stepLength, yOffset, Math.Min(sampleAt, sampleCount - 1), ch);
                        drawingContext.DrawLine(BiosignalPen, prev, current);
                        prev = current;
                        if (sampleAt >= sampleCount - 1) break;
                    }
                }
                BiosignalVisualElement.Children.Add(drawingVisual);
            }

            var t0 = _biosignalRecords[sampleOffset].Timestamp;
            var t1 = _biosignalRecords[sampleOffset + sampleCount - 1].Timestamp;
            var markers = (ModulePluginComboBox.SelectedItem as Plugin)?.Markers;
            var markerBrushes = new Dictionary<int, Brush>();
            if (markers != null)
                foreach (var markerId in markers.Keys)
                    markerBrushes[markerId] = new SolidColorBrush(markers[markerId].Color.ToSwmColor());
            var markerDrawingVisual = new DrawingVisual();
            using (var drawingContext = markerDrawingVisual.RenderOpen())
                foreach (var mkr in _markerRecords.InTimeRange(t0, t1))
                {
                    if (!markerBrushes.TryGetValue(mkr.Marker, out var brush)) continue;
                    var x = (mkr.Timestamp - t0) / (double) (t1 - t0) * clientSize.Width;
                    var pen = new Pen(brush, 1) {DashStyle = new DashStyle(new double[] {1, 1, 1}, 1), DashCap = PenLineCap.Flat};
                    drawingContext.DrawLine(pen, new Point(x, 0), new Point(x, clientSize.Height));
                }
            BiosignalVisualElement.Children.Add(markerDrawingVisual);
        }

        private void UpdateMarkers()
        {
            var markerRecordItems = new List<MarkerRecordItem>(_markerRecords.Count);
            var markers = ((ModulePluginComboBox.SelectedItem as FrameworkElement)?.Tag as Plugin)?.Markers;
            MarkerRecord previous = null;
            foreach (var record in _markerRecords)
            {
                var item = new MarkerRecordItem
                {
                    Marker = record.Marker,
                    Timestamp = record.Timestamp,
                    Definition = markers == null ? null : markers.TryGetValue(record.Marker, out var definition) ? definition.FullName : null,
                    DeltaT = previous == null ? 0 : record.Timestamp - previous.Timestamp
                };
                previous = record;
                markerRecordItems.Add(item);
            }
            MarkerRecordListView.ItemsSource = markerRecordItems;
        }

        private ICollection<Pair<MarkerRecord>> Ranges(int startMarker, int endMarker)
        {
            if (startMarker == endMarker) throw new ArgumentException();
            var linkedList = new LinkedList<Pair<MarkerRecord>>();
            if (_markerRecords == null || !_markerRecords.Any()) return linkedList;
            MarkerRecord start = null;
            foreach (var markerRecord in _markerRecords.Where(r => r.Marker == startMarker || r.Marker == endMarker))
            {
                if (markerRecord.Marker == startMarker)
                    start = markerRecord;
                else if (markerRecord.Marker == endMarker && start != null)
                {
                    linkedList.AddLast(new Pair<MarkerRecord>(start, markerRecord));
                    start = null;
                }
            }
            return linkedList;
        } 

        private void ModuleComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) => UpdateMarkers();

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        private void GenerateEpochs_OnClick(object sender, RoutedEventArgs e)
        {
            var datFile = _dataFilePrefix + ".dat";

            if (!File.Exists(datFile))
                throw new UserException("Data file not found");
            if (!int.TryParse(StartMarkerTextBox.Text, out var startMarker))
                throw new UserException($"Start marker invalid: '{StartMarkerTextBox.Text}'");
            if (!int.TryParse(EndMarkerTextBox.Text, out var endMarker))
                throw new UserException($"End marker invalid: '{EndMarkerTextBox.Text}'");
            if (!ulong.TryParse(DelayTextBox.Text, out var delay))
                throw new UserException($"Delay invalid: '{DelayTextBox.Text}'");

            var ranges = Ranges(startMarker, endMarker);
            if (!ranges.Any())
            {
                MessageBox.Show("No epochs found");
                return;
            }

            var epochDir = _dataFilePrefix + $"-marker[{startMarker}-{endMarker},d{delay}]/";
            Directory.CreateDirectory(epochDir);

            var epoch = 0;
            var lines = new LinkedList<string>();

            void WriteEpoch()
            {
                if (lines.IsEmpty()) return;
                
                File.WriteAllLines(epochDir + $"epoch{++epoch}.dat", lines, Encoding.ASCII);
                lines.Clear();
            }

            using (var lineEnumerator = File.ReadLines(datFile).GetEnumerator())
            using (var rangeEnumerator = ranges.GetEnumerator())
            {
                string line = null;
                Pair<MarkerRecord> range = null;
                for (;;)
                {
                    if (line == null)
                    {
                        if (lineEnumerator.MoveNext())
                            line = lineEnumerator.Current;
                        else
                            break;
                    }

                    if (range == null)
                    {
                        if (rangeEnumerator.MoveNext())
                            range = rangeEnumerator.Current;
                        else
                            break;
                    }

                    var lastComma = line.LastIndexOf(',');
                    var timestamp = ulong.Parse(line.Substring(lastComma + 1));
                    if (timestamp > range.Right.Timestamp + delay)
                    {
                        range = null;
                        WriteEpoch();
                    }
                    else if (timestamp < range.Left.Timestamp + delay)
                        line = null;
                    else
                    {
                        lines.AddLast(line);
                        line = null;
                    }
                }
            }

            MessageBox.Show($"{epoch} epochs generated");
            Process.Start(epochDir);
        }

        private void AnalysisWindow_OnLoaded(object sender, RoutedEventArgs e)
        {
            DelayedUpdateSignals();
        }

        private void AnalysisWindow_OnSizeChanged(object sender, SizeChangedEventArgs e) => DelayedUpdateSignals();

        private void MenuItem_OnClick(object sender, RoutedEventArgs e) => new VideoFrameAnalysisWindow(_dataFilePrefix).Show();

        private void BiosignalPrevPageButton_OnClick(object sender, RoutedEventArgs e)
        {
            _biosignalPage--;
            DelayedUpdateSignals();
        }

        private void BiosignalNextPageButton_OnClick(object sender, RoutedEventArgs e)
        {
            _biosignalPage++;
            DelayedUpdateSignals();
        }

        private void BiosignalFirstPageButton_OnClick(object sender, RoutedEventArgs e)
        {
            _biosignalPage = 0;
            DelayedUpdateSignals();
        }

        private void BiosignalLastPageButton_OnClick(object sender, RoutedEventArgs e)
        {
            _biosignalPage = (int)PageCount - 1;
            DelayedUpdateSignals();
        }

        private void BiosignalSubWindowSizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            _biosignalWindowSize -= 100;
            DelayedUpdateSignals();
        }

        private void BiosignalAddWindowSizeButton_OnClick(object sender, RoutedEventArgs e)
        {
            _biosignalWindowSize += 100;
            DelayedUpdateSignals();
        }

    }

}
