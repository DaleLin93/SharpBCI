using System;
using MarukoLib.Interop;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices.MarkerSources;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.Extensions.IO.Consumers.Marker
{

    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class MarkerParallelPortSetter : StreamConsumer<Timestamped<IMarker>>
    {

        public const string ConsumerName = "Marker Parallel Port Setter";

        public class Factory : StreamConsumerFactory<Timestamped<IMarker>>
        {

            private static readonly ITypeConverter Short2HexConverter = TypeConverter<short, string>.Of(
                num => $"0x{num:X}", str => Convert.ToInt16(str, str.StartsWith("0x") ? 16 : 10));

            public static readonly Parameter<short> ParallelPortAddressParam = Parameter<short>.CreateBuilder("Port Address", 0x378)
                .SetMetadata(Presenters.Presenters.PresenterProperty, TypeConvertedPresenter.Instance)
                .SetMetadata(Presenters.Presenters.PresentTypeConverterProperty, Short2HexConverter)
                .SetMetadata(TypeConvertedPresenter.ConvertedContextProperty, new ContextBuilder()
                    .SetProperty(SelectablePresenter.CustomizableProperty, true)
                    .BuildReadonly())
                .SetSelectableValues(new short[] {0x378, 0x278, 0x3BC})
                .Build();

            public static readonly Parameter<bool> InvertBitsParam = MarkerParallelPortWriter.Factory.InvertBitsParam;

            public static readonly Parameter<bool> ReverseBitsParam = MarkerParallelPortWriter.Factory.ReverseBitsParam;

            public Factory() : base(ParallelPortAddressParam, InvertBitsParam, ReverseBitsParam) { }

            public override IStreamConsumer<Timestamped<IMarker>> Create(Session session, IReadonlyContext context, byte? num)
                => new MarkerParallelPortSetter(ParallelPortAddressParam.Get(context), InvertBitsParam.Get(context), ReverseBitsParam.Get(context));

        }

        public MarkerParallelPortSetter(short portAddress, bool invertBits, bool reverseBits)
        {
            PortAddress = portAddress;
            InvertBits = invertBits;
            ReverseBits = reverseBits;
        }

        public short PortAddress { get; }

        public bool InvertBits { get; }

        public bool ReverseBits { get; }

        public void SendEvent(byte b)
        {
            b = InvertBits ? b.InvertBits() : b;
            b = ReverseBits ? b.ReverseBits() : b;
            InpOut32.Out32(PortAddress, b);
        }

        public override void Accept(Timestamped<IMarker> value) => SendEvent((byte)(value.Value.Code & 0xFF));

    }
}
