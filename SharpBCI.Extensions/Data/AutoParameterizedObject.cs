using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{

    public interface IAutoParamAdapter
    {

        bool IsValid(FieldInfo field, object value);

    }

    [AttributeUsage(AttributeTargets.Field)]
    public class AutoParamAttribute : Attribute
    {

        private static readonly IDictionary<Type, IAutoParamAdapter> Adapters = new Dictionary<Type, IAutoParamAdapter>();

        public AutoParamAttribute([NotNull] string name, [CanBeNull] string unit = null, [CanBeNull] string desc = null, [CanBeNull] Type adapterType = null)
        {
            Name = name;
            Unit = unit;
            Desc = desc;
            Adapter = GetAdapter(adapterType);
        }

        [CanBeNull]
        private static IAutoParamAdapter GetAdapter([CanBeNull] Type adapterType)
        {
            if (adapterType == null) return null;
            if (Adapters.TryGetValue(adapterType, out var adapter)) return adapter;
            return Adapters[adapterType] = (IAutoParamAdapter)adapterType.InitClassOrStruct();
        }

        [NotNull] public string Name { get; }

        [CanBeNull] public string Unit { get; }

        [CanBeNull] public string Desc { get; }

        [CanBeNull] public IAutoParamAdapter Adapter { get; }

    }

    public sealed class AutoParameterizedObjectFactory : IParameterizedObjectFactory
    {

        private class AutoParam : IParameterDescriptor
        {

            internal AutoParam(FieldInfo field, AutoParamAttribute attribute)
            {
                Field = field;
                Attribute = attribute;
            }

            public FieldInfo Field { get; }

            public AutoParamAttribute Attribute { get; }

            public Type ValueType => Field.FieldType;

            public string Name => Attribute.Name;

            public string Description => Attribute.Desc;

            public string Key => ParameterUtils.GenerateKey(Attribute.Name);

            public string Unit => Attribute.Unit;

            public bool IsNullable => ValueType.IsNullableType();

            public object DefaultValue => Activator.CreateInstance(ValueType);

            public IEnumerable SelectableValues => ValueType.IsEnum ? Enum.GetValues(ValueType) : null;

            public ITypeConverter TypeConverter => null;

            public IReadonlyContext Metadata => EmptyContext.Instance;

            public bool IsValid(object value) => IsNullable && value == null || ValueType.IsInstanceOfType(value) && (Attribute.Adapter?.IsValid(Field, value) ?? true);

        }

        private struct AutoParameterizedObjectMeta
        {

            [NotNull] public readonly Func<IParameterizedObject> Constructor;

            [NotNull] public readonly AutoParam[] Parameters;

            public AutoParameterizedObjectMeta([NotNull] Func<IParameterizedObject> constructor, [NotNull] AutoParam[] parameters)
            {
                Constructor = constructor;
                Parameters = parameters;
            }

        }

        private readonly IDictionary<Type, AutoParameterizedObjectMeta> _autoParameters = new Dictionary<Type, AutoParameterizedObjectMeta>();

        // ReSharper disable once SuggestBaseTypeForParameter
        private AutoParameterizedObjectMeta GetMeta(IParameterDescriptor parameter)
        {
            if (_autoParameters.TryGetValue(parameter.ValueType, out var result)) return result;
            if (!typeof(IParameterizedObject).IsAssignableFrom(parameter.ValueType)) throw new ArgumentException("IParameterizedObject interface is required");
            Func<IParameterizedObject> constructor;
            if (parameter.ValueType.IsClass)
            {
                var classConstructor = parameter.ValueType.GetConstructor(EmptyArray<Type>.Instance) ?? throw new ArgumentException("no-arg constructor is required for class");
                constructor = () => (IParameterizedObject)classConstructor.Invoke(EmptyArray<object>.Instance);
            }
            else
                constructor = () => (IParameterizedObject)Activator.CreateInstance(parameter.ValueType);
            var parameters = new LinkedList<AutoParam>();
            foreach (var field in parameter.ValueType.GetFields())
            {
                if (field.IsStatic || field.IsInitOnly) continue;
                var attribute = field.GetCustomAttribute<AutoParamAttribute>();
                if (attribute == null) continue;
                parameters.AddLast(new AutoParam(field, attribute));
            }
            return _autoParameters[parameter.ValueType] = new AutoParameterizedObjectMeta(constructor, parameters.ToArray());
        }

        public IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter) => GetMeta(parameter).Parameters.ToArray<IParameterDescriptor>();

        public bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => true;

        public IParameterizedObject Create(IParameterDescriptor parameter, IReadonlyContext context)
        {
            var meta = GetMeta(parameter);
            var value = meta.Constructor();
            foreach (var autoParameter in meta.Parameters) autoParameter.Field.SetValue(value, context.TryGet(autoParameter, out var pv) ? pv : autoParameter.DefaultValue);
            return value;
        }

        public IReadonlyContext Parse(IParameterDescriptor parameter, IParameterizedObject parameterizedObject)
        {
            var meta = GetMeta(parameter);
            var context = new Context(meta.Parameters.Length);
            foreach (var autoParameter in meta.Parameters) context[autoParameter] = autoParameter.Field.GetValue(parameterizedObject);
            return context;
        }

    }

    [ParameterizedObject(typeof(AutoParameterizedObjectFactory))]
    public interface IAutoParameterizedObject : IParameterizedObject { }

}