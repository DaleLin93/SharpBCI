using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.IO;

namespace SharpBCI.Extensions.IO.Devices.MarkerSources
{

    /// <summary>
    /// Mark streamer.
    /// Streaming manually marked value.
    /// </summary>
    public class MarkerStreamer : TimestampedStreamer<IMarker>, IMarkable
    {

        public sealed class Factory : IStreamerFactory
        {

            public bool IsDeviceDependent => false;

            public Type StreamingType => typeof(Timestamped<IMarker>);

            public IStreamer Create(IDevice device, IClock clock) => new MarkerStreamer((IMarkerSource)device, clock);

        }

        [CanBeNull] public readonly IMarkerSource MarkerSource;

        public MarkerStreamer([CanBeNull] IMarkerSource markerSource, [NotNull] IClock clock) : base(nameof(MarkerStreamer), clock)
        {
            MarkerSource = markerSource;
            Started += (sender, e) => MarkerSource?.Open();
            Stopped += (sender, e) => MarkerSource?.Shutdown();
        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        public MarkerStreamer([CanBeNull] IMarkerSource markerSource, [NotNull] IClock clock, [NotNull] IConsumer<Timestamped<IMarker>> consumer)
            : this(markerSource, clock) => AttachConsumer(consumer);

        public long Mark(string label, int marker)
        {
            var obj = WithTimestamp(new Marker(label, marker));
            Enqueue(obj);
            return obj.Timestamp;
        }

        protected override Timestamped<IMarker> Acquire() => WithTimestamp(MarkerSource?.Read() ?? throw new EndOfStreamException());

    }
}
