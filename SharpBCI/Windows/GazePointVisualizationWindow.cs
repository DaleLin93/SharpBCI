using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using MarukoLib.DirectX;
using MarukoLib.Lang;
using D2D1 = SharpDX.Direct2D1;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Devices;
using SharpBCI.Extensions.Streamers;
using SharpDX.Mathematics.Interop;
using Color = SharpDX.Color;
using RenderForm = MarukoLib.DirectX.RenderForm;

namespace SharpBCI.Windows
{

    internal class GazePointVisualizationWindow : RenderForm, IStreamConsumer<Timestamped<IGazePoint>>
    {

        private readonly GazePointStreamer _streamer;

        private readonly long _historyCount;

        /* Data */

        private readonly LinkedList<RawVector2> _gazePoints = new LinkedList<RawVector2>();

        public GazePointVisualizationWindow(GazePointStreamer streamer, int historyCount)
        {
            // ReSharper disable once LocalizableElement
            Text = "Gaze-Point Visualization";
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

            _historyCount = historyCount;
        }

        public void Accept(Timestamped<IGazePoint> value)
        {
            lock (_gazePoints)
            {
                var gazePoint = value.Value;
                _gazePoints.AddFirst(new RawVector2((float) gazePoint.X, (float) gazePoint.Y));
                while (_gazePoints.Count > _historyCount) _gazePoints.RemoveLast();
            }
        }

        protected override void Draw(D2D1.RenderTarget renderTarget)
        {
            renderTarget.Clear(Color.Black);

            var screenRects = Screen.AllScreens.Select(screen => screen.Bounds)
                .Select(SharpDXUtils.ToRawRectangleF)
                .ToArray();
            var displayBounds = screenRects.GetBounds();
            var clientSize = ClientSize;
            var scale = Math.Min(clientSize.Width / displayBounds.Width(), clientSize.Height / displayBounds.Height()) * 0.9F;

            var pixelDrawingScale = 1 / scale;

            var lineThickness = pixelDrawingScale * 2;
            var pointSize = pixelDrawingScale * 5;

            renderTarget.Transform = ((RawMatrix3x2) SharpDX.Matrix3x2.Identity)
                .Translate(clientSize.Width / 2F, clientSize.Height / 2F)
                .Scale(scale).Translate(displayBounds.Center().Multiply(-1));

            SolidColorBrush.Color = Color.White;
            foreach (var rect in screenRects) renderTarget.DrawRectangle(rect, SolidColorBrush, lineThickness);

            lock (_gazePoints)
            {
                var count = 0;
                foreach (var gazePoint in _gazePoints)
                {
                    var alpha = 1 - (float) count++ / _historyCount;
                    SolidColorBrush.Color = new RawColor4(1, 0, 0, alpha * alpha);
                    renderTarget.FillEllipse(new D2D1.Ellipse(gazePoint, pointSize, pointSize), SolidColorBrush);
                }
            }
        }

        private void Window_OnLoaded(object sender, EventArgs e) => _streamer.Start();

        private void Window_OnClosing(object sender, EventArgs e) => _streamer.Stop();

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
        }

        Type IStreamConsumer.AcceptType => typeof(Timestamped<IGazePoint>);

        StreamConsumerPriority IStreamConsumer.Priority => StreamConsumerPriority.Highest;

        void IStreamConsumer.Accept(object value) => Accept((Timestamped<IGazePoint>)value);

    }
}
