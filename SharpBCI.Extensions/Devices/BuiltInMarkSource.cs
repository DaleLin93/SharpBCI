using MarukoLib.Lang;

namespace SharpBCI.Extensions.Devices
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public sealed class BuiltInMarkSource : MarkSource
    {

        public const string DeviceName = "Built-in Source";

        public class Factory : DeviceFactory<BuiltInMarkSource, IMarkSource>
        {

            public override BuiltInMarkSource Create(IReadonlyContext context) => Instance;

        }

        public static readonly BuiltInMarkSource Instance = new BuiltInMarkSource();

        private BuiltInMarkSource() { }

        public override void Open() { }

        public override IMark Read() => null;

        public override void Shutdown() { }

        public override void Dispose() { }

    }

}
