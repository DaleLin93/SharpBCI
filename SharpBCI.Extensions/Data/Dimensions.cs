using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{

    [ParameterizedObject(typeof(Factory))]
    public struct Dimensions : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<Dimensions>
        {

            public static readonly NamedProperty<string[]> DimensionNamesProperty = new NamedProperty<string[]>("DimensionNames", new[] {"X", "Y"});

            private readonly IDictionary<string, Parameter<ushort>> _cachedParameters = new Dictionary<string, Parameter<ushort>>();

            public override IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter) => 
                DimensionNamesProperty.Get(parameter.Metadata).Select(GetParameter).ToArray<IParameterDescriptor>();

            public override Dimensions Create(IParameterDescriptor parameter, IReadonlyContext context)
            {
                var dimensionNames = DimensionNamesProperty.Get(parameter.Metadata);
                var dimensions = new ushort[dimensionNames.Length];
                for (var i = 0; i < dimensionNames.Length; i++)
                    dimensions[i] = GetParameter(dimensionNames[i]).Get(context);
                return new Dimensions(dimensions, false);
            }

            public override IReadonlyContext Parse(IParameterDescriptor parameter, Dimensions dimensions)
            {
                var context = new Context();
                var dimensionNames = DimensionNamesProperty.Get(parameter.Metadata);
                for (var i = 0; i < dimensionNames.Length; i++)
                    context[GetParameter(dimensionNames[i])] = dimensions[i];
                return context;
            }

            private Parameter<ushort> GetParameter(string name) => _cachedParameters.GetOrCreate(name, key => new Parameter<ushort>(name, 1));

        }

        [CanBeNull] private readonly ushort[] _values;

        public Dimensions(params ushort[] values) : this(values, true) { }

        private Dimensions(ushort[] values, bool clone) => _values = (clone ? (ushort[])values?.Clone() : values) ?? EmptyArray<ushort>.Instance;

        public ushort this[int index] => _values == null || index < 0 || index >= _values.Length ? (ushort) 1 : _values[index];

        public int Count => _values?.Length ?? 0;

        public ulong Volume => _values?.Aggregate((ulong) 1, (current, val) => current * val) ?? 0;

        public ushort[] Values => (ushort[])_values?.Clone() ?? EmptyArray<ushort>.Instance;

    }

}