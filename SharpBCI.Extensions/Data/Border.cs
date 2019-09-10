using System.Drawing;
using MarukoLib.Lang;
using MarukoLib.UI;

namespace SharpBCI.Extensions.Data
{
    [ParameterizedObject(typeof(Factory))]
    public struct Border : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<Border>
        {

            private static readonly Parameter<double> Width = new Parameter<double>("Width", null, "0 - No border, border color will be ignored", Predicates.Nonnegative, 1);

            private static readonly Parameter<Color> Color = new Parameter<Color>("Color", System.Drawing.Color.White);

            public override Border Create(IParameterDescriptor parameter, IReadonlyContext context) => new Border(Width.Get(context), Color.Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, Border border) => new Context
            {
                [Width] = border.Width,
                [Color] = border.Color.ToSdColor()
            };

        }

        public readonly double Width;

        public readonly uint Color;

        public Border(double width, Color color) : this(width, color.ToUIntArgb()) { }

        public Border(double width, uint color)
        {
            Width = width;
            Color = color;
        }

    }
}