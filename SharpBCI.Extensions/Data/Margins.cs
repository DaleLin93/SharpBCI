using System;
using System.Windows;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{

    [ParameterizedObject(typeof(Factory))]
    public struct Margins : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<Margins>
        {

            public static readonly NamedProperty<bool> RelativeProperty = new NamedProperty<bool>("Relative");

            private static readonly Parameter<bool> Relative = new Parameter<bool>("Relative", "false - Absolute, true - Relative", false);

            private static readonly Parameter<double>[] Edges =
            {
                new Parameter<double>("Left", unit: "Absolute - dp, Relative - ratio", null, 0),
                new Parameter<double>("Top", unit: "Absolute - dp, Relative - ratio", null, 0),
                new Parameter<double>("Right", unit: "Absolute - dp, Relative - ratio", null, 0),
                new Parameter<double>("Bottom", unit: "Absolute - dp, Relative - ratio", null, 0)
            };

            public override Margins Create(IParameterDescriptor parameter, IReadonlyContext context) => new Margins(
                RelativeProperty.GetOrDefault(parameter.Metadata, Relative.Get(context)),
                Edges[0].Get(context), Edges[1].Get(context), Edges[2].Get(context), Edges[3].Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, Margins margins) => new Context
            {
                [Relative] = margins.Relative,
                [Edges[0]] = margins.Left,
                [Edges[1]] = margins.Top,
                [Edges[2]] = margins.Right,
                [Edges[3]] = margins.Bottom
            };

        }

        public readonly bool Relative;

        public readonly double Left, Top, Right, Bottom;

        public Margins(bool relative, double margin)
        {
            Relative = relative;
            Left = margin;
            Top = margin;
            Right = margin;
            Bottom = margin;
        }

        public Margins(bool relative, double left, double top, double right, double bottom)
        {
            Relative = relative;
            Left = left;
            Top = top;
            Right = right;
            Bottom = bottom;
        }

        public Margins(Thickness thickness)
        {
            Relative = true;
            Left = thickness.Left;
            Top = thickness.Top;
            Right = thickness.Right;
            Bottom = thickness.Bottom;
        }

        public static Margins operator *(Margins margins, double multiplier) =>
            new Margins(margins.Relative, margins.Left * multiplier, margins.Top * multiplier, margins.Right * multiplier, margins.Bottom * multiplier);

        public static Margins operator /(Margins margins, double divider) =>
            new Margins(margins.Relative, margins.Left / divider, margins.Top / divider, margins.Right / divider, margins.Bottom / divider);

        public bool IsEmpty(double tolerance = 0) => Math.Abs(Left) < tolerance && Math.Abs(Top) < tolerance && Math.Abs(Right) < tolerance && Math.Abs(Bottom) < tolerance;

        public Margins GetAbsolute(double size) => GetAbsolute(size, size);

        public Margins GetAbsolute(double width, double height) => Relative ? new Margins(false, Left * width, Top * height, Right * width, Bottom * height) : this;

        public Margins GetRelative(double size) => GetRelative(size, size);

        public Margins GetRelative(double width, double height) => Relative ? this : new Margins(true, Left / width, Top / height, Right / width, Bottom / height);

    }

}