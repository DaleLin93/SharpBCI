using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using MarukoLib.Lang;

namespace SharpBCI.Extensions
{

    public interface IAutoParamAdapter
    {

        bool IsValid(FieldInfo field, object value);

    }

    [AttributeUsage(AttributeTargets.Field)]
    public class AutoParamAttribute : Attribute
    {

        private static readonly IDictionary<Type, IAutoParamAdapter> Adapters = new Dictionary<Type, IAutoParamAdapter>();

        public AutoParamAttribute() { }

        public AutoParamAttribute([CanBeNull] string name) => Name = name;

        public AutoParamAttribute([CanBeNull] string key, [CanBeNull] string name)
        {
            Key = key;
            Name = name;
        }

        [CanBeNull]
        private static IAutoParamAdapter GetAdapter([CanBeNull] Type adapterType)
        {
            if (adapterType == null) return null;
            if (Adapters.TryGetValue(adapterType, out var adapter)) return adapter;
            return Adapters[adapterType] = (IAutoParamAdapter)Activator.CreateInstance(adapterType);
        }

        [CanBeNull] public string Key { get; set; }

        [CanBeNull] public string Name { get; set; }

        [CanBeNull] public string Unit { get; set; }

        [CanBeNull] public string Desc { get; set; }

        [CanBeNull] public Type AdapterType { get; set; }

        [CanBeNull] public IAutoParamAdapter Adapter => GetAdapter(AdapterType);

    }

    internal class AutoParameter : IParameterDescriptor
    {

        internal AutoParameter(FieldInfo field, AutoParamAttribute attribute)
            : this(field, attribute, Activator.CreateInstance(field.FieldType)) { }

        internal AutoParameter(FieldInfo field, AutoParamAttribute attribute, object defaultValue)
            : this(field, attribute, defaultValue, field.FieldType.IsEnum ? Enum.GetValues(field.FieldType) : null) { }

        public AutoParameter(FieldInfo field, AutoParamAttribute attribute,
            object defaultValue, IEnumerable selectableValues)
        {
            Field = field;
            Attribute = attribute;
            DefaultValue = defaultValue;
            SelectableValues = selectableValues;
        }

        public FieldInfo Field { get; }

        public AutoParamAttribute Attribute { get; }

        public string Key => Attribute.Key ?? ParameterUtils.GenerateKeyByName(Name);

        public string Name => Attribute.Name ?? Field.Name;

        public string Unit => Attribute.Unit;

        public string Description => Attribute.Desc;

        public Type ValueType => Field.FieldType;

        public bool IsNullable => ValueType.IsNullableType();

        public object DefaultValue { get; }

        public IEnumerable SelectableValues { get; }

        public IReadonlyContext Metadata { get; set; } = EmptyContext.Instance;

        public bool IsValid(object value) => IsNullable && value == null || ValueType.IsInstanceOfType(value) && (Attribute.Adapter?.IsValid(Field, value) ?? true);

    }

}
