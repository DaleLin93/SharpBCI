using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using MarukoLib.Lang;
using SharpBCI.Core.IO;

namespace SharpBCI.Extensions.IO.Devices.EyeTrackers
{

    /// <summary>
    /// Gaze point streamer.
    /// Streaming gaze point data from specific eye-tracker.
    /// </summary>
    public class GazePointStreamer : TimestampedStreamer<IGazePoint>
    {

        public sealed class Factory : IStreamerFactory
        {

            public bool IsDeviceDependent => true;

            public Type StreamingType => typeof(Timestamped<IGazePoint>);

            public IStreamer Create(IDevice device, IClock clock) => new GazePointStreamer((IEyeTracker)device ?? throw new ArgumentNullException(nameof(device)), clock);

        }

        public readonly IEyeTracker EyeTracker;

        public GazePointStreamer(IEyeTracker eyeTracker, IClock clock) : base(nameof(GazePointStreamer), clock)
        {
            EyeTracker = eyeTracker;
            Started += (sender, e) => eyeTracker.Open();
            Stopped += (sender, e) => eyeTracker.Shutdown();
        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        public GazePointStreamer(IEyeTracker eyeTracker, IClock clock, IConsumer<Timestamped<IGazePoint>> consumer) : this(eyeTracker, clock) => AttachConsumer(consumer);

        protected override Timestamped<IGazePoint> Acquire() => WithTimestamp(EyeTracker.Read() ?? throw new EndOfStreamException());

    }

}
