using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using DirectShowLib;
using MarukoLib.Interop;

namespace SharpBCI.Paradigms.MI
{

    internal static class Tools
    {

        /// <summary>Get filter's pin.</summary>
        /// <param name="filter">Filter to get pin of.</param>
        /// <param name="dir">Pin's direction.</param>
        /// <param name="num">Pin's number.</param>
        /// <returns>Returns filter's pin.</returns>
        public static IPin GetPin(IBaseFilter filter, PinDirection dir, int num)
        {
            var pins = new IPin[1];
            if (filter.EnumPins(out var enumPins) == 0)
            {
                try
                {
                    while (enumPins.Next(1, pins, IntPtr.Zero) == 0)
                    {
                        pins[0].QueryDirection(out var pinDirection);
                        if (pinDirection == dir)
                        {
                            if (num == 0)
                                return pins[0];
                            --num;
                        }
                        Marshal.ReleaseComObject(pins[0]);
                        pins[0] = null;
                    }
                }
                finally
                {
                    Marshal.ReleaseComObject(enumPins);
                }
            }
            return null;
        }

        /// <summary>Get filter's input pin.</summary>
        /// <param name="filter">Filter to get pin of.</param>
        /// <param name="num">Pin's number.</param>
        /// <returns>Returns filter's pin.</returns>
        public static IPin GetInPin(IBaseFilter filter, int num) => GetPin(filter, PinDirection.Input, num);

        /// <summary>Get filter's output pin.</summary>
        /// <param name="filter">Filter to get pin of.</param>
        /// <param name="num">Pin's number.</param>
        /// <returns>Returns filter's pin.</returns>
        public static IPin GetOutPin(IBaseFilter filter, int num) => GetPin(filter, PinDirection.Output, num);

    }

    public class DirectShowVideoSource : IDisposable
    {

        private class Grabber : ISampleGrabberCB
        {

            private readonly DirectShowVideoSource _parent;

            private Bitmap _bitmap;

            public int Width { get; set; }

            public int Height { get; set; }

            public Grabber(DirectShowVideoSource parent) => _parent = parent;

            public int SampleCB(double sampleTime, IMediaSample pSample) => 0;

            public unsafe int BufferCB(double sampleTime, IntPtr buffer, int bufferLen)
            {
                if (_parent.NewFrame != null)
                {
                    if (_bitmap == null || _bitmap.Width != Width || _bitmap.Height != Height)
                    {
                        _bitmap?.Dispose();
                        _bitmap = new Bitmap(Width, Height, PixelFormat.Format24bppRgb);
                    }
                    var bitmapData = _bitmap.LockBits(new Rectangle(0, 0, Width, Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
                    var stride1 = bitmapData.Stride;
                    var stride2 = bitmapData.Stride;
                    var dst = (byte*)((IntPtr)bitmapData.Scan0.ToPointer() + stride2 * (Height - 1));
                    var pointer = (byte*)buffer.ToPointer();
                    for (var index = 0; index < Height; ++index)
                    {
                        NtDll.memcpy(dst, pointer, stride1);
                        dst -= stride2;
                        pointer += stride1;
                    }
                    _bitmap.UnlockBits(bitmapData);
                    _parent.OnNewFrame(_bitmap);
                }
                return 0;
            }
        }

        public const int WmGraphNotify = 0x00008001;  // message from graph

        public EventHandler<Bitmap> NewFrame;

        private readonly object _lock = new object();

        private readonly Thread _thread;

        private FilterGraph _filterGraph;

        private SampleGrabber _sampleGrabber;

        private IBaseFilter _sourceFilter;

        private IMediaSeeking _mediaSeeking;

        private IMediaControl _mediaControl;

        private IMediaEventEx _mediaEventEx;

        public DirectShowVideoSource(Uri uri, bool repeat)
        {
            Uri = uri;
            Repeat = repeat;

            _filterGraph = new FilterGraph();
            var graphBuilder = (IGraphBuilder)_filterGraph;
            graphBuilder.AddSourceFilter(Uri.LocalPath, "source", out _sourceFilter);
            if (_sourceFilter == null) throw new ApplicationException("Failed creating source filter");
            _sampleGrabber = new SampleGrabber();
            var sampleGrabber = (ISampleGrabber)_sampleGrabber;
            var grabFilter = (IBaseFilter)sampleGrabber;
            graphBuilder.AddFilter(grabFilter, "grabber");
            var mediaType = new AMMediaType { majorType = MediaType.Video, subType = MediaSubType.RGB24 };
            sampleGrabber.SetMediaType(mediaType);
            var num = 0;
            var inPin = Tools.GetInPin(grabFilter, 0);
            IPin outPin;
            while (true)
            {
                outPin = Tools.GetOutPin(_sourceFilter, num);
                if (outPin != null)
                {
                    if (graphBuilder.Connect(outPin, inPin) < 0)
                    {
                        Marshal.ReleaseComObject(outPin);
                        ++num;
                    }
                    else
                        goto label_12;
                }
                else
                    break;
            }
            Marshal.ReleaseComObject(inPin);
            throw new ApplicationException("Did not find acceptable output video pin in the given source");
            label_12:
            Marshal.ReleaseComObject(outPin);
            Marshal.ReleaseComObject(inPin);
            var grabber = new Grabber(this);
            if (sampleGrabber.GetConnectedMediaType(mediaType) == 0)
            {
                var structure = (VideoInfoHeader)Marshal.PtrToStructure(mediaType.formatPtr, typeof(VideoInfoHeader));
                grabber.Width = structure.BmiHeader.Width;
                grabber.Height = structure.BmiHeader.Height;

                if (mediaType.formatSize != 0 && mediaType.formatPtr != IntPtr.Zero)
                {
                    Marshal.FreeCoTaskMem(mediaType.formatPtr);
                    mediaType.formatSize = 0;
                }

                if (mediaType.unkPtr != IntPtr.Zero)
                {
                    Marshal.Release(mediaType.unkPtr);
                    mediaType.unkPtr = IntPtr.Zero;
                }
            }
            graphBuilder.Render(Tools.GetOutPin(grabFilter, 0));
            ((IVideoWindow)_filterGraph).put_AutoShow(OABool.False);
            sampleGrabber.SetBufferSamples(false);
            sampleGrabber.SetOneShot(false);
            sampleGrabber.SetCallback(grabber, 1);
            //                if (!this.referenceClockEnabled)
            //                    ((IMediaFilter)o1).SetSyncSource((IReferenceClock)null);
            _mediaSeeking = (IMediaSeeking)_filterGraph;
            _mediaControl = (IMediaControl)_filterGraph;
            _mediaEventEx = (IMediaEventEx)_filterGraph;

            _thread = new Thread(() =>
            {
                do
                {
                    int result;
                    EventCode eventCode;
                    lock (_lock)
                        if ((result = _mediaEventEx.GetEvent(out eventCode, out var lParam1, out var lParam2, 0)) >= 0)
                            _mediaEventEx.FreeEventParams(eventCode, lParam1, lParam2);
                    if (result < 0)
                    {
                        try
                        {
                            Thread.Sleep(200);
                        }
                        catch (Exception)
                        {
                            // ignored
                        }
                    }
                    if (eventCode == EventCode.Complete)
                    {
                        if (repeat)
                            lock (_lock)
                                Rewind();
                        else
                            break;
                    }
                } while (true);
            });
            _thread.Start();
        }

        public Uri Uri { get; }

        public bool Repeat { get; }

        public void Play()
        {
            lock (_lock)
                _mediaControl.Run();
        }

        public void Pause()
        {
            lock (_lock)
                _mediaControl.Pause();
        }

        public void Stop()
        {
            lock (_lock)
                _mediaControl.Stop();
        }

        public void Rewind()
        {
            lock (_lock)
                _mediaSeeking.SetPositions(new DsLong(0), AMSeekingSeekingFlags.AbsolutePositioning, null, AMSeekingSeekingFlags.NoPositioning);
        }

        public void Reset(bool pause = true)
        {
            Rewind();
            lock (_lock)
            {
                if (pause)
                    _mediaControl.Pause();
                else
                    _mediaControl.Run();
            }
        }

        public void Dispose()
        {
            _thread.Abort();
            _mediaSeeking = null;
            _mediaControl = null;
            _mediaEventEx = null;
            if (_filterGraph != null) Marshal.ReleaseComObject(_filterGraph);
            _filterGraph = null;
            if (_sourceFilter != null) Marshal.ReleaseComObject(_sourceFilter);
            _sourceFilter = null;
            if (_sampleGrabber != null) Marshal.ReleaseComObject(_sampleGrabber);
            _sampleGrabber = null;
        }

        internal void OnNewFrame(Bitmap bitmap) => NewFrame?.Invoke(this, bitmap);

    }


}
