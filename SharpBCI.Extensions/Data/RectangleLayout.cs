using System;
using System.Collections.Generic;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{

    [ParameterizedObject(typeof(Factory))]
    public struct RectangleLayout : IParameterizedObject
    {
        public class Factory : ParameterizedObjectFactory<RectangleLayout>
        {

            public static readonly ContextProperty<bool> SquareProperty = new ContextProperty<bool>();

            public static readonly ContextProperty<bool> UnifyMarginProperty = new ContextProperty<bool>();

            private static readonly Parameter<uint> Size = new Parameter<uint>("Size", "dp", "0 - automatically fill screen", 100);

            private static readonly Parameter<uint> Width = new Parameter<uint>("Width", "dp", "0 - automatically fill screen", 100);

            private static readonly Parameter<uint> Height = new Parameter<uint>("Height", "dp", "0 - automatically fill screen", 100);

            private static readonly Parameter<uint> Margin = new Parameter<uint>("Margin", "dp", "0 - no margin", 0);

            private static readonly Parameter<uint> HMargin = new Parameter<uint>("H-Margin", "dp", "0 - no margin", 0);

            private static readonly Parameter<uint> VMargin = new Parameter<uint>("V-Margin", "dp", "0 - no margin", 0);

            public override IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter)
            {
                var descriptors = new LinkedList<IParameterDescriptor>();
                if (SquareProperty.GetOrDefault(parameter.Metadata, false))
                    descriptors.AddLast(Size);
                else
                {
                    descriptors.AddLast(Width);
                    descriptors.AddLast(Height);
                }
                if (SquareProperty.GetOrDefault(parameter.Metadata, false))
                    descriptors.AddLast(Margin);
                else
                {
                    descriptors.AddLast(HMargin);
                    descriptors.AddLast(VMargin);
                }
                return descriptors.AsReadonly();
            }

            public override RectangleLayout Create(IParameterDescriptor parameter, IReadonlyContext context)
            {
                uint width, height, hMargin, vMargin;
                if (SquareProperty.GetOrDefault(parameter.Metadata, false))
                    width = height = Size.Get(context);
                else
                {
                    width = Width.Get(context);
                    height = Height.Get(context);
                }
                if (SquareProperty.GetOrDefault(parameter.Metadata, false))
                    hMargin = vMargin = Margin.Get(context);
                else
                {
                    hMargin = HMargin.Get(context);
                    vMargin = VMargin.Get(context);
                }
                return new RectangleLayout(width, height, hMargin, vMargin);
            }

            public override IReadonlyContext Parse(IParameterDescriptor parameter, RectangleLayout layout)
            {
                return new Context
                {
                    [Size] = layout.Width,
                    [Width] = layout.Width,
                    [Height] = layout.Height,
                    [Margin] = layout.HMargin,
                    [HMargin] = layout.HMargin,
                    [VMargin] = layout.VMargin
                };
            }

        }

        public readonly uint Width, Height;

        public readonly uint HMargin, VMargin;

        public RectangleLayout(uint size, uint margin) : this(size, size, margin, margin) { }

        public RectangleLayout(uint width, uint height, uint hMargin, uint vMargin)
        {
            Width = width;
            Height = height;
            HMargin = hMargin;
            VMargin = vMargin;
        }

        public uint Size
        {
            get
            {
                if (Width != Height) throw new Exception("'Width' and 'Height' were different");
                return Width;
            }
        }

        public uint Margin
        {
            get
            {
                if (HMargin != VMargin) throw new Exception("'H-Margin' and 'V-Margin' were different");
                return HMargin;
            }
        }
    }
}