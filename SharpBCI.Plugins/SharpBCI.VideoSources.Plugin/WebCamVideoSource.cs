using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using MarukoLib.Lang;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices;
using SharpBCI.Extensions.Presenters;
using IVideoSource = SharpBCI.Extensions.Devices.IVideoSource;

namespace SharpBCI.VideoSources
{

    public class WebCamVideoSource : VideoSource
    {

        public const string DeviceName = "Web-Camera";

        public class Factory : DeviceFactory<WebCamVideoSource, IVideoSource>
        {

            public struct DeviceInfo
            {

                public readonly int Index;

                public readonly string Moniker, Name;

                public DeviceInfo(int index, string moniker, string name)
                {
                    Index = index;
                    Moniker = moniker;
                    Name = name;
                }

                public override string ToString() => $"{Index}.{Name} ({Moniker})";

            }

            public static readonly Parameter<DeviceInfo> DeviceParam = Parameter<DeviceInfo>.CreateBuilder("Device")
                .SetMetadata(Presenters.PresenterProperty, SelectablePresenter.Instance)
                .SetMetadata(SelectablePresenter.SelectableValuesFuncProperty, p => GetVideoCaptureDeviceMonikers())
                .Build();

            public Factory() : base(WebCamVideoSource.DeviceName, DeviceParam) { }

            [SuppressMessage("ReSharper", "CollectionNeverUpdated.Local")]
            private static IEnumerable<DeviceInfo> GetVideoCaptureDeviceMonikers()
            {
                var filterInfoCollection = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                var identifiers = new DeviceInfo[filterInfoCollection.Count];
                for (var i = 0; i < filterInfoCollection.Count; i++)
                {
                    var filterInfo = filterInfoCollection[i];
                    identifiers[i] = new DeviceInfo(i, filterInfo.MonikerString, filterInfo.Name); 
                }
                return identifiers;
            }

            public override WebCamVideoSource Create(IReadonlyContext context) => new WebCamVideoSource(DeviceParam.Get(context).Moniker);

        }

        private readonly object _lock = new object();

        private readonly AutoResetEvent _signal = new AutoResetEvent(false);

        private readonly AsyncVideoSource _videoSource;

        private volatile bool _stopped = true;

        private volatile IVideoFrame _frame;

        public WebCamVideoSource(string moniker) : base(DeviceName)
        {
            Device = new VideoCaptureDevice(moniker ?? throw new ArgumentNullException(nameof(moniker)));
            _videoSource = new AsyncVideoSource(Device);
            var videoCapability = Device.VideoCapabilities[0];
            FrameSize = videoCapability.FrameSize;
            MaxFrameRate = videoCapability.MaximumFrameRate;
        }

        public VideoCaptureDevice Device { get; }

        public override Size FrameSize { get; }

        public override double MaxFrameRate { get; }

        public override void Open()
        {
            _signal.Reset();
            _stopped = false;
            _videoSource.Start();
            _videoSource.NewFrame += Device_NewFrame;
        }

        public override void Shutdown()
        {
            _videoSource.NewFrame -= Device_NewFrame;
            _videoSource.Stop();
            _stopped = true;
            _frame = null;
            _signal.Set();
        }

        public override IVideoFrame Read()
        {
            while (!_stopped)
            {
                lock (_lock)
                {
                    if (!_signal.WaitOne(500)) continue;
                    return _frame;
                }
            }
            return null;
        }

        private void Device_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            _frame = new VideoFrame(eventArgs.Frame);
            _signal.Set();
        }

    }

}
