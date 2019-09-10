using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{
    
    [ParameterizedObject(typeof(Factory))]
    public struct ComplexObject : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<ComplexObject>
        {

            public static readonly NamedProperty<IParameterDescriptor[]> FieldsProperty = new NamedProperty<IParameterDescriptor[]>("Fields", EmptyArray<IParameterDescriptor>.Instance);

            public override IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter) => FieldsProperty.Get(parameter.Metadata);

            public override ComplexObject Create(IParameterDescriptor parameter, IReadonlyContext context)
            {
                var fields = FieldsProperty.Get(parameter.Metadata);
                if (fields.IsEmpty()) return Empty;
                var values = new object[fields.Length];
                for (var i = 0; i < fields.Length; i++)
                    values[i] = context[fields[i]];
                return new ComplexObject(fields, values);
            }

            public override IReadonlyContext Parse(IParameterDescriptor parameter, ComplexObject complexObject)
            {
                var fields = FieldsProperty.Get(parameter.Metadata);
                if (fields.IsEmpty()) return EmptyContext.Instance;
                var context = new Context();
                for (var i = 0; i < fields.Length; i++)
                {
                    var field = fields[i];
                    context[field] = complexObject.Lookup(field, i);
                }
                return context;
            }

        }

        public static readonly ComplexObject Empty = new ComplexObject();

        [CanBeNull] private readonly KeyValuePair<IParameterDescriptor, object>[] _pairs;

        public ComplexObject(params object[] values) : this(null, values) { }

        public ComplexObject(IParameterDescriptor[] keys, object[] values)
        {
            keys = keys ?? EmptyArray<IParameterDescriptor>.Instance;
            values = values ?? EmptyArray<object>.Instance;
            _pairs = new KeyValuePair<IParameterDescriptor, object>[values.Length];
            for (var i = 0; i < values.Length; i++)
                _pairs[i] = new KeyValuePair<IParameterDescriptor, object>(i < 0 || i >= keys.Length ? null : keys[i], values[i]);
        }

        public ComplexObject(KeyValuePair<IParameterDescriptor, object>[] pairs) : this(pairs, true) { }

        private ComplexObject(KeyValuePair<IParameterDescriptor, object>[] pairs, bool clone) => _pairs = clone ? (KeyValuePair<IParameterDescriptor, object>[])pairs?.Clone() : pairs;

        public object this[int index] => GetOrDefault(index, null);

        public object this[string name] => GetOrDefault(name, null);

        public object this[IParameterDescriptor parameter]
        {
            get
            {
                if (_pairs != null)
                    foreach (var pair in _pairs)
                        if (Equals(pair.Key, parameter))
                            return pair.Value;
                return parameter.DefaultValue;
            }
        }

        public int Count => _pairs?.Length ?? 0;

        public T[] GetValues<T>() => _pairs?.Select(p => p.Value).Cast<T>().ToArray() ?? EmptyArray<T>.Instance;

        public T GetOrDefault<T>(int index, T defaultValue) => GetOrDefault(index, (object) defaultValue) is T t ? t : defaultValue;

        public object GetOrDefault(int index, object defaultValue) => _pairs == null || index < 0 || index >= _pairs.Length ? defaultValue : _pairs[index].Value;

        public T GetOrDefault<T>(string name, T defaultValue) => GetOrDefault(name, (object)defaultValue) is T t ? t : defaultValue;

        public object GetOrDefault(string name, object defaultValue)
        {
            if (_pairs != null)
                foreach (var pair in _pairs)
                    if (Equals(pair.Key?.Key, name))
                        return pair.Value;
            return defaultValue;
        }

        public T Get<T>(Parameter<T> parameter) => this[parameter] is T t ? t : default;

        public object Lookup([NotNull] IParameterDescriptor parameter, int index)
        {
            if (_pairs != null)
                foreach (var pair in _pairs)
                    if (Equals(pair.Key, parameter))
                        return pair.Value;
            return GetOrDefault(index, parameter.DefaultValue);
        }

    }

}