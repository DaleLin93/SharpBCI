using System;
using MarukoLib.Interop;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices.MarkerSources;

namespace SharpBCI.Extensions.IO.Consumers
{

    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class MarkerParallelPortWriter : StreamConsumer<Timestamped<IMarker>>
    {

        public const string ConsumerName = "Marker Parallel Port Writer";

        public class Factory : StreamConsumerFactory<Timestamped<IMarker>>
        {

            public static readonly ITypeConverter UShort2HexConverter =
                TypeConverter<ushort, string>.Of(num => $"0x{num:X}", hex => Convert.ToUInt16(hex, 16));

            public static readonly Parameter<ushort> ParallelPortAddressParam = Parameter<ushort>
                .CreateBuilder("Parallel Port Address")
                .SetSelectableValues(new ushort[] { 0x278, 0x378, 0x3BC })
                .SetMetadata(Presenters.Presenters.PresentTypeConverterProperty, UShort2HexConverter)
                .Build();

            public Factory() : base(ParallelPortAddressParam) { }

            public override IStreamConsumer<Timestamped<IMarker>> Create(Session session, IReadonlyContext context, byte? num)
                => new MarkerParallelPortWriter(ParallelPortAddressParam.Get(context));

        }

        private readonly IntPtr _portAddress;

        public MarkerParallelPortWriter(uint portAddress) => _portAddress = (IntPtr)portAddress;

        public bool SendEvent(byte b)
        {
            var written = 0;
            Kernel32.OVERLAPPED overlapped = null;
            return Kernel32.WriteFile(_portAddress, new[] { b }, 1, ref written, ref overlapped) && written > 0;
        }

        public override void Accept(Timestamped<IMarker> value) => SendEvent((byte)(value.Value.Code & 0xFF));

    }
}
