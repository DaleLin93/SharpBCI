using System;
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

    internal class VideoFramePresentationWindow : RenderForm, IConsumer<Timestamped<IVideoFrame>>
    {

        private readonly VideoFrameStreamer _streamer;

        /* Data */

        private D2D1.Bitmap _bitmap = null;

        public VideoFramePresentationWindow(VideoFrameStreamer streamer)
        {
            // ReSharper disable once LocalizableElement
            Text = "Video Frame Presentation";
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
        }

        public void Accept(Timestamped<IVideoFrame> value)
        {
            if (RenderTarget == null) return;
            lock (this)
            lock (RenderContextLock)
            {
//                if (_bitmap == null)
//                    _bitmap = value.Value.Frame.ToD2D1Bitmap(RenderTarget);
//                else
//                    value.Value.Frame.CopyToD2D1Bitmap(_bitmap);
            }
        }

        protected override void Draw(D2D1.RenderTarget renderTarget)
        {
            renderTarget.Clear(Color.Black);
            lock (this)
                if (_bitmap != null)
                {
                    var scale = Math.Min(ClientSize.Width / _bitmap.Size.Width, ClientSize.Height / _bitmap.Size.Height);
                    var scaledWidth = _bitmap.Size.Width * scale;
                    var scaledHeight = _bitmap.Size.Height * scale;
                    var left = (ClientSize.Width - scaledWidth) / 2;
                    var top = (ClientSize.Height - scaledHeight) / 2;
                    var destRect = new RawRectangleF(left, top, left + scaledWidth, top + scaledHeight);
                    renderTarget.DrawBitmap(_bitmap, destRect, 1, D2D1.BitmapInterpolationMode.NearestNeighbor);
                }
        }

        protected override void DisposeDirectXResources()
        {
            lock (this)
            {
                _bitmap?.Dispose();
                _bitmap = null;
            }
            base.DisposeDirectXResources();
        }

        private void Window_OnLoaded(object sender, EventArgs e) => _streamer.Start();

        private void Window_OnClosing(object sender, EventArgs e) => _streamer.Stop();

        private void Window_OnKeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape) Close();
        }

        Type IConsumer.AcceptType => typeof(Timestamped<IVideoFrame>);

        ConsumerPriority IConsumer.Priority => ConsumerPriority.Highest;

        void IConsumer.Accept(object value) => Accept((Timestamped<IVideoFrame>)value);

    }
}
