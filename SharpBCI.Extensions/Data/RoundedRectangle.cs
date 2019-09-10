using System;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{

    [ParameterizedObject(typeof(Factory))]
    public struct RoundedRectangle : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<RoundedRectangle>
        {

            private static readonly Parameter<double> Width = new Parameter<double>("Width", "dp", null, Predicates.Nonnegative, 300);

            private static readonly Parameter<double> Height = new Parameter<double>("Height", "dp", null, Predicates.Nonnegative, 300);

            private static readonly Parameter<double> CornerRadius = new Parameter<double>("Corner Radius", "dp", null, Predicates.Nonnegative, 0);

            public override RoundedRectangle Create(IParameterDescriptor parameter, IReadonlyContext context) => 
                new RoundedRectangle(Width.Get(context), Height.Get(context), CornerRadius.Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, RoundedRectangle roundedRectangle) => new Context
            {
                [Width] = roundedRectangle.Width,
                [Height] = roundedRectangle.Height,
                [CornerRadius] = roundedRectangle.CornerRadius,
            };

        }

        public readonly double Width, Height, CornerRadius;

        public RoundedRectangle(double width, double height, double cornerRadius) 
        {
            Width = width;
            Height = height;
            CornerRadius = cornerRadius;
        }

        public bool IsSquare(double tolerance) => Math.Abs(Width - Height) < tolerance;

    }

}