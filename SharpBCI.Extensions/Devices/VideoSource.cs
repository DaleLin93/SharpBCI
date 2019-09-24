using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using JetBrains.Annotations;
using MarukoLib.IO;
using SharpBCI.Extensions.Streamers;

namespace SharpBCI.Extensions.Devices
{

    public interface IVideoFrame
    {

        int Width { get; }

        int Height { get; }

        Bitmap ToBitmap();

        void Write(Stream stream);

    }

    public struct VideoFrame : IVideoFrame
    {

        private readonly Bitmap _frame;

        public VideoFrame([NotNull] Bitmap bitmap) : this(bitmap, true) { }

        private VideoFrame([NotNull] Bitmap bitmap, bool clone)
        {
            _frame = clone ? (Bitmap) bitmap.Clone() : bitmap;
            Width = bitmap.Width;
            Height = bitmap.Height;
        }

        public static VideoFrame Read(Stream stream)
        {
            var tmp = Path.GetTempFileName() + ".png";
            using (var fileStream = new FileStream(tmp, FileMode.CreateNew))
                stream.CopyTo(fileStream);
            var bitmap = new Bitmap(tmp);
            File.Delete(tmp);
            return new VideoFrame(bitmap, false);
        }

        public int Width { get; }

        public int Height { get; }

        public Bitmap ToBitmap() => _frame;

        public void Write(Stream stream) => _frame.Save(stream, ImageFormat.Png);

    }

    public struct RawByteVideoFrame : IVideoFrame
    {

        private readonly byte[] _frame;

        public RawByteVideoFrame([NotNull] Bitmap bitmap)
        {
            var rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            var bytes = new byte[rect.Width * rect.Height * sizeof(int)];

            // Lock System.Drawing.Bitmap
            var bitmapData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);

            // Convert all pixels.
            for (var y = 0; y < rect.Height; y++)
            {
                var offset = bitmapData.Stride * y;
                for (var x = 0; x < rect.Width; x++)
                {
                    // Not optimized.
                    var B = Marshal.ReadByte(bitmapData.Scan0, offset + 0);
                    var G = Marshal.ReadByte(bitmapData.Scan0, offset + 1);
                    var R = Marshal.ReadByte(bitmapData.Scan0, offset + 2);
                    var A = Marshal.ReadByte(bitmapData.Scan0, offset + 3);
                    bytes[offset + 0] = A;
                    bytes[offset + 1] = R;
                    bytes[offset + 2] = G;
                    bytes[offset + 3] = B;
                    offset += 4;
                }
            }
            bitmap.UnlockBits(bitmapData);
            _frame = bytes;
            Width = rect.Width;
            Height = rect.Height;
            BytesPerPixel = 4;
        }

        internal RawByteVideoFrame([NotNull] byte[] frame, int width, int height)
        {
            _frame = frame;
            Width = width;
            Height = height;
            BytesPerPixel = frame.Length / width / height;
        }

        public static RawByteVideoFrame Read(Stream stream)
        {
            var bytes = new byte[sizeof(int)];
            stream.ReadFully(bytes);
            var width = bytes.ReadInt32FromNetworkOrder();
            stream.ReadFully(bytes);
            var height = bytes.ReadInt32FromNetworkOrder();
            stream.ReadFully(bytes);
            var bytesPerPixel = bytes.ReadInt32FromNetworkOrder();
            bytes = new byte[width * height * bytesPerPixel];
            stream.ReadFully(bytes);
            return new RawByteVideoFrame(bytes, width, height);
        }

        public int Width { get; }

        public int Height { get; }

        public int BytesPerPixel { get; }

        public Bitmap ToBitmap()
        {
            var bitmap = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            var rect = new Rectangle(0, 0, Width, Height);
            var bytes = _frame;

            var bitmapData = bitmap.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            // Convert all pixels.
            for (var y = 0; y < rect.Height; y++)
            {
                var offset = bitmapData.Stride * y;
                for (var x = 0; x < rect.Width; x++)
                {
                    // Not optimized.
                    var B = bytes[offset + 3]; //Marshal.ReadByte(bitmapData.Scan0, offset + 0);
                    var G = bytes[offset + 2]; //Marshal.ReadByte(bitmapData.Scan0, offset + 1);
                    var R = bytes[offset + 1]; //Marshal.ReadByte(bitmapData.Scan0, offset + 2);
                    var A = bytes[offset + 0]; //Marshal.ReadByte(bitmapData.Scan0, offset + 3);

                    Marshal.WriteByte(bitmapData.Scan0, offset + 0, B);
                    Marshal.WriteByte(bitmapData.Scan0, offset + 1, G);
                    Marshal.WriteByte(bitmapData.Scan0, offset + 2, R);
                    Marshal.WriteByte(bitmapData.Scan0, offset + 3, A);
                    offset += 4;
                }
            }
            bitmap.UnlockBits(bitmapData);
            return bitmap;
        }

        public void Write(Stream stream)
        {
            var bytes = new byte[sizeof(int)];
            bytes.WriteInt32AsNetworkOrder(Width);
            stream.WriteFully(bytes, false);
            bytes.WriteInt32AsNetworkOrder(Height);
            stream.WriteFully(bytes, false);
            bytes.WriteInt32AsNetworkOrder(BytesPerPixel);
            stream.WriteFully(bytes, false);
            stream.WriteFully(_frame);
        } 

    }

    [DeviceType("videoSource", "Video Source", StreamerFactoryType = typeof(VideoFrameStreamer.Factory))]
    public interface IVideoSource : IDevice
    {

        Size FrameSize { get; }

        double MaxFrameRate { get; }

        IVideoFrame Read();

    }

    public abstract class VideoSource : Device, IVideoSource
    {

        public abstract Size FrameSize { get; }

        public abstract double MaxFrameRate { get; }

        public abstract IVideoFrame Read();

    }

}
