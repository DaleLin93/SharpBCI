using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using JetBrains.Annotations;
using MarukoLib.Image;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Devices
{

    public class ScreenCapturer : VideoSource
    {

        public const string DeviceName = "Screen Capturer";

        public class Factory : DeviceFactory<ScreenCapturer, IVideoSource>
        {

            public static readonly Parameter<byte> MaxFpsParam = new Parameter<byte>("Max FPS", 25);

            public static readonly Parameter<int> ScreenParam = new Parameter<int>("Screen", -1);

            public static readonly Parameter<int> WidthParam = new Parameter<int>("Width", -1);

            public static readonly Parameter<int> HeightParam = new Parameter<int>("Height", -1);

            public Factory() : base(ScreenCapturer.DeviceName, MaxFpsParam, ScreenParam, WidthParam, HeightParam) { }

            public override ScreenCapturer Create(IReadonlyContext context) => 
                new ScreenCapturer(MaxFpsParam.Get(context), ScreenParam.Get(context), WidthParam.Get(context), HeightParam.Get(context));

        }

        private const PixelFormat PixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb;

        private readonly object _lock = new object();

        private readonly int _screenIndex, _targetWidth, _targetHeight;

        private readonly long _minimumInterval;

        private int _frameWidth, _frameHeight;

        private Rectangle _cloneRect;

        private long? _lastTimestamp;

        private Bitmap _capturedBitmap, _scaledBitmap;

        public ScreenCapturer(byte maxFps, int screenIndex, int width, int height) : base(DeviceName)
        {
            _screenIndex = screenIndex;
            _targetWidth = width;
            _targetHeight = height;

            MaxFrameRate = maxFps;

            _minimumInterval = 1000 / maxFps;
        }

        [CanBeNull]
        private static Screen GetScreen(int idx)
        {
            if (idx < 0) return Screen.PrimaryScreen;
            var screens = Screen.AllScreens;
            return screens.Length > idx ? screens[idx] : null;
        }

        public override Size FrameSize => new Size(_frameWidth, _frameHeight);

        public override double MaxFrameRate { get; }

        public override void Open()
        {
            var screen = GetScreen(_screenIndex) ?? throw new ArgumentException($"screen not found by index: {_screenIndex}");
            _frameWidth = _targetWidth <= 0 ? screen.Bounds.Width : _targetWidth;
            _frameHeight = _targetHeight <= 0 ? screen.Bounds.Height : _targetHeight;
            _cloneRect = new Rectangle(0, 0, _frameWidth, _frameHeight);
            _lastTimestamp = null;
        }

        public override IVideoFrame Read()
        {
            lock (_lock)
            {
                while (_lastTimestamp != null && (DateTimeUtils.CurrentTimeMillis - _lastTimestamp.Value) < _minimumInterval) { }
                Screen screen;
                while ((screen = GetScreen(_screenIndex)) == null) { } // busy wait
                _capturedBitmap = screen.TakeScreenshot(PixelFormat, _capturedBitmap);
                _scaledBitmap = _capturedBitmap.ScaleToSize(_frameWidth, _frameHeight, _scaledBitmap);
                _lastTimestamp = DateTimeUtils.CurrentTimeMillis;
                return new VideoFrame(_scaledBitmap.Clone(_cloneRect, _scaledBitmap.PixelFormat));
            }
        }

        public override void Shutdown() { }

    }
}
