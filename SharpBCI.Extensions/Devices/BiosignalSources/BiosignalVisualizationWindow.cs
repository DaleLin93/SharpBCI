using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MarukoLib.DirectX;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Streamers;
using SharpDX.Mathematics.Interop;
using DW = SharpDX.DirectWrite;
using D2D1 = SharpDX.Direct2D1;
using Color = SharpDX.Color;

namespace SharpBCI.Extensions.Devices.BiosignalSources
{

    internal class BiosignalVisualizationWindow : Direct2DForm, IStreamConsumer<Timestamped<ISample>>
    {

        private readonly BiosignalStreamer _streamer;

        private readonly long _channelNum;

        private readonly long _windowLength;

        /* Data */

        private readonly LinkedList<double[]> _values = new LinkedList<double[]>();

        /* DirectX Resources */

        private DW.TextFormat _textFormat;

        public BiosignalVisualizationWindow(BiosignalStreamer streamer, long channelNum, long windowLength)
        {
            // ReSharper disable once LocalizableElement
            Text = "Biosignal Visualization";
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location);

            SuspendLayout();
            ControlBox = true;
            IsFullscreen = false;
            DoubleBuffered = false;
            ResumeLayout(true);

            Load += Window_OnLoaded;
            Closing += Window_OnClosing;
            KeyUp += Window_OnKeyUp;

            _streamer = streamer;
            streamer.Attach(this);

            _channelNum = channelNum;
            _windowLength = windowLength;
        }

        public void Accept(Timestamped<ISample> value)
        {
            lock (_values)
            {
                _values.AddLast(value.Value.Values);
                if (_values.Count > _windowLength)
                    _values.RemoveFirst();
            }
        }

        protected override void InitializeDirectXResources()
        {
            base.InitializeDirectXResources();
            _textFormat = new DW.TextFormat(DwFactory, "Arial", DW.FontWeight.Bold,
                DW.FontStyle.Normal, DW.FontStretch.Normal, 84 * (float)GraphicsUtils.Scale)
            {
                TextAlignment = DW.TextAlignment.Center,
                ParagraphAlignment = DW.ParagraphAlignment.Center
            };
        }

        protected override void DisposeDirectXResources()
        {
            _textFormat.Dispose();
            base.DisposeDirectXResources();
        }

        protected override void Draw(D2D1.RenderTarget renderTarget)
        {
            renderTarget.Clear(Color.Black);

            double[][] samples;
            lock (_values)
                samples = _values.ToArray();
            var sampleCount = samples.Length;

            double range;
            double[] chMeans;
            {
                var channelSums = new double[_channelNum];
                var max = double.NegativeInfinity;
                var min = double.PositiveInfinity;
                foreach (var channels in samples)
                    for (var ch = 0; ch < channels.Length; ch++)
                    {
                        var channel = channels[ch];
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

            var clientSize = ClientSize;
            var channelHeight = clientSize.Height / (float) _channelNum;
            var sampleInterval = clientSize.Width / (float) _windowLength;

            RawVector2 Point(float xOffset, float yOffset, int sIdx, int chIdx) =>
                new RawVector2(xOffset, (float) (yOffset + channelHeight / 2 + (samples[sIdx][chIdx] - chMeans[chIdx]) / range * channelHeight / 2 * 0.9));

            SolidColorBrush.Color = Color.White;
            if (!samples.IsEmpty())
                for (var ch = 0; ch < _channelNum; ch++)
                {
                    var yOffset = ch * channelHeight;
                    var xOffset = (float) Width;
                    var s = samples.Length - 1;
                    var prev = Point(xOffset, yOffset, s, ch);
                    for (; s >= 0; s--)
                    {
                        xOffset -= sampleInterval;
                        var current = Point(xOffset, yOffset, s, ch);
                        renderTarget.DrawLine(prev, current, SolidColorBrush, 2);
                        prev = current;
                    }
                }
        }

        private void Window_OnLoaded(object sender, EventArgs e) => _streamer.Start();

        private void Window_OnClosing(object sender, EventArgs e) => _streamer.Stop();

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
        }

        Type IStreamConsumer.AcceptType => typeof(Timestamped<ISample>);

        StreamConsumerPriority IStreamConsumer.Priority => StreamConsumerPriority.Highest;

        void IStreamConsumer.Accept(object value) => Accept((Timestamped<ISample>)value);

    }
}
