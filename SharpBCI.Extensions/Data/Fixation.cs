using System.Drawing;
using System.Windows;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Extensions.Presenters;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Data
{

    [ParameterizedObject(typeof(Factory))]
    public struct Fixation : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<Fixation>
        {

            private static readonly Parameter<uint> Size = Parameter<uint>.CreateBuilder("Size")
                .SetUnit("dp").SetDescription("0 - hidden").SetDefaultValue(5)
                .SetMetadata(ParameterizedObjectPresenter.ColumnWidthProperty, ViewConstants.Star1GridLength)
                .Build();

            private static readonly Parameter<Color> Color = Parameter<Color>.CreateBuilder("Color")
                .SetDefaultValue(System.Drawing.Color.White)
                .SetMetadata(ParameterizedObjectPresenter.ColumnWidthProperty, new GridLength(2, GridUnitType.Star))
                .Build();

            public override Fixation Create(IParameterDescriptor parameter, IReadonlyContext context) => new Fixation(Size.Get(context), Color.Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, Fixation fixation) => new Context
            {
                [Size] = fixation.Size,
                [Color] = fixation.Color.ToSdColor()
            };

        }

        public readonly uint Size;

        public readonly uint Color;

        public Fixation(uint size, Color color) : this(size, color.ToUIntArgb()) { }

        public Fixation(uint size, uint color)
        {
            Size = size;
            Color = color;
        }

    }

}