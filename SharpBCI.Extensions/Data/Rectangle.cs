using System;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{
    [ParameterizedObject(typeof(Factory))]
    public struct Rectangle : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<Rectangle>
        {

            private static readonly Parameter<double> Width = new Parameter<double>("Width", "dp", null, Predicates.Nonnegative, 300);

            private static readonly Parameter<double> Height = new Parameter<double>("Height", "dp", null, Predicates.Nonnegative, 300);

            public override Rectangle Create(IParameterDescriptor parameter, IReadonlyContext context) => new Rectangle(Width.Get(context), Height.Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, Rectangle rectangle) => new Context
            {
                [Width] = rectangle.Width,
                [Height] = rectangle.Height,
            };

        }

        public readonly double Width, Height;

        public Rectangle(double width, double height) 
        {
            Width = width;
            Height = height;
        }

        public bool IsSquare(double tolerance) => Math.Abs(Width - Height) < tolerance;

    }
}