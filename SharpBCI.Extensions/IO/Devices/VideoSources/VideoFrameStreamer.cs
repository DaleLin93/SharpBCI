using System;
using System.IO;
using MarukoLib.Lang;
using SharpBCI.Core.IO;

namespace SharpBCI.Extensions.IO.Devices.VideoSources
{

    /// <summary>
    /// Video frame streamer.
    /// Streaming video frame data from specific video source.
    /// </summary>
    public class VideoFrameStreamer : TimestampedStreamer<IVideoFrame>
    {

        public sealed class Factory : IStreamerFactory
        {

            public Type StreamingType => typeof(Timestamped<IVideoFrame>);

            public IStreamer Create(IDevice device, IClock clock) => device == null ? null : new VideoFrameStreamer((IVideoSource)device, clock);

        }

        public readonly IVideoSource VideoSource;

        public VideoFrameStreamer(IVideoSource videoSource, IClock clock) : base(nameof(VideoFrameStreamer), clock)
        {
            VideoSource = videoSource;
            Started += (sender, e) => videoSource.Open();
            Stopped += (sender, e) => videoSource.Shutdown();
        }

        public VideoFrameStreamer(IVideoSource videoSource, IClock clock, IConsumer<Timestamped<IVideoFrame>> consumer) : this(videoSource, clock) => Attach(consumer);

        protected override Timestamped<IVideoFrame> Acquire() => WithTimestamp(VideoSource.Read() ?? throw new EndOfStreamException());

    }
}
