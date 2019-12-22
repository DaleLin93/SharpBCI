using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{
    [ParameterizedObject(typeof(Factory))]
    public struct Rectangle : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<Rectangle>
        {

            public const string DefaultUnit = "dp";

            public static readonly NamedProperty<string> UnitProperty = new NamedProperty<string>("Unit");

            private static readonly Parameter<double> Width = new Parameter<double>("Width", DefaultUnit, null, Predicates.Nonnegative, 300);

            private static readonly Parameter<double> Height = new Parameter<double>("Height", DefaultUnit, null, Predicates.Nonnegative, 300);

            private static readonly IParameterDescriptor[] DefaultParameters = {Width, Height};

            private readonly ConditionalWeakTable<IParameterDescriptor, IParameterDescriptor[]> _cache;

            public Factory() => _cache = new ConditionalWeakTable<IParameterDescriptor, IParameterDescriptor[]>();

            public override IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter) => GetParameterArray(parameter);

            public override Rectangle Create(IParameterDescriptor parameter, IReadonlyContext context)
            {
                var parameters = GetParameterArray(parameter);
                return new Rectangle(context.GetOrDefault(parameters[0], Width.DefaultValue), context.GetOrDefault(parameters[1], Height.DefaultValue));
            }

            public override IReadonlyContext Parse(IParameterDescriptor parameter, Rectangle rectangle)
            {
                var parameters = GetParameterArray(parameter);
                return new Context
                {
                    [parameters[0]] = rectangle.Width,
                    [parameters[1]] = rectangle.Height,
                };
            } 

            private IParameterDescriptor[] GetParameterArray(IParameterDescriptor parameter)
            {
                if (!UnitProperty.TryGet(parameter.Metadata, out var unit) || Equals(unit, DefaultUnit)) return DefaultParameters;
                if (_cache.TryGetValue(parameter, out var cachedParameters)) return cachedParameters;
                var metadata = new Context {[UnitProperty] = unit};
                var parameters = new IParameterDescriptor[]
                {
                    new MetadataOverridenParameter(Width, metadata), 
                    new MetadataOverridenParameter(Height, metadata)
                };
                _cache.Add(parameter, parameters);
                return parameters;
            }

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