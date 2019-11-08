using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.IO.Devices.VideoSources;

namespace SharpBCI.Windows
{
    /// <summary>
    /// Interaction logic for VideoFrameAnalysisWindow.xaml
    /// </summary>
    public partial class VideoFrameAnalysisWindow
    {

        private struct VideoFrameHeader : IRecord
        {

            public VideoFrameHeader(long position, ulong index, ulong timestamp)
            {
                Index = index;
                Timestamp = timestamp;
                Position = position;
            }

            public ulong Index { get; }

            public ulong Timestamp { get; }

            public long Position { get; }

        }

        private readonly string _dataFilePrefix;

        private readonly VideoFrameHeader[] _frameHeaders;

        private int _currentFrameIndex = 0;

        public VideoFrameAnalysisWindow(string dataFilePrefix)
        {
            _dataFilePrefix = dataFilePrefix;
            _frameHeaders = LoadVideoFrameHeaders(dataFilePrefix);

            InitializeComponent();
        }

        private static VideoFrameHeader[] LoadVideoFrameHeaders(string dataFilePrefix)
        {
            var file = dataFilePrefix + VideoFramesFileWriter.FileSuffix;
            if (!File.Exists(file)) return EmptyArray<VideoFrameHeader>.Instance;
            var videoFrameRecords = new LinkedList<VideoFrameHeader>();
            uint index = 0;
            using (var fileStream = new FileStream(file, FileMode.Open))
                while (fileStream.Position < fileStream.Length)
                {
                    var pos = fileStream.Position;
                    var timestamp = VideoFrameRecord.ReadTimestampAndSkip(fileStream);
                    videoFrameRecords.AddLast(new VideoFrameHeader(pos, index++, timestamp));
                }
            return videoFrameRecords.OrderByTimestamp().ToArray();
        }

        private void UpdateFrame()
        {
            _currentFrameIndex = Math.Max(0, Math.Min(_frameHeaders.Length - 1, _currentFrameIndex));
            FrameIndexTextBlock.Text = $"{_currentFrameIndex + 1}/{_frameHeaders.Length}";

            if (_frameHeaders.IsEmpty()) return;
            var header = _frameHeaders[_currentFrameIndex];
            IVideoFrame frame;
            using (var stream = new FileStream(_dataFilePrefix + VideoFramesFileWriter.FileSuffix, FileMode.Open))
            {
                stream.Position = header.Position;
                frame = VideoFrameRecord.Read(stream, header.Index).Frame;
            }
            VideoFrameImage.Source = frame.ToBitmap().ToBitmapSource();
            FrameIndexTextBlock.Text = $"{_currentFrameIndex + 1}/{_frameHeaders.Length} ({frame.Width}×{frame.Height}, T:{header.Timestamp})";
        }

        private void Window_OnLoaded(object sender, RoutedEventArgs e) => UpdateFrame();

        private void PrevFrameButton_OnClick(object sender, RoutedEventArgs e)
        {
            _currentFrameIndex--;
            UpdateFrame();
        }
        
        private void NextFrameButton_OnClick(object sender, RoutedEventArgs e)
        {
            _currentFrameIndex++;
            UpdateFrame();
        }

        private void FirstFrameButton_OnClick(object sender, RoutedEventArgs e)
        {
            _currentFrameIndex = 0;
            UpdateFrame();
        }

        private void LastFrameButton_OnClick(object sender, RoutedEventArgs e)
        {
            _currentFrameIndex = _frameHeaders.Length - 1;
            UpdateFrame();
        }

    }
}
