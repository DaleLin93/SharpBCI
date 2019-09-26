using MarukoLib.Lang;

namespace SharpBCI.Extensions.Devices.MarkerSources
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public sealed class EmptyMarkerSource : MarkerSource
    {

        public const string DeviceName = "Empty Source";

        public class Factory : DeviceFactory<EmptyMarkerSource, IMarkerSource>
        {

            public override EmptyMarkerSource Create(IReadonlyContext context) => Instance;

        }

        public static readonly EmptyMarkerSource Instance = new EmptyMarkerSource();

        private EmptyMarkerSource() { }

        public override void Open() { }

        public override IMarker Read() => null;

        public override void Shutdown() { }

        public override void Dispose() { }

    }

}
