using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using MarukoLib.Lang;

namespace SharpBCI.Extensions
{

    public interface IDescriptor
    {

        [CanBeNull] string Name { get; }

        [CanBeNull] string Description { get; }

    }

    public interface IParameterDescriptor : IContextProperty, IDescriptor
    {

        [NotNull] string Key { get; }

        [CanBeNull] string Unit { get; }

        bool IsNullable { get; }

        [CanBeNull] object DefaultValue { get; }

        [CanBeNull] IEnumerable SelectableValues { get; }

        [NotNull] IReadonlyContext Metadata { get; }

        bool IsValid([CanBeNull] object value);

    }

    public interface IGroupDescriptor : IDescriptor
    {

        [NotNull] IReadOnlyCollection<IDescriptor> Items { get; }

    }

    public interface IRoutedParameter : IParameterDescriptor
    {

        [NotNull] IParameterDescriptor OriginalParameter { get; }

    }

    public sealed class ParameterGroup : IGroupDescriptor, IReadOnlyCollection<IDescriptor>
    {

        public ParameterGroup([NotNull] params IDescriptor[] items) 
            : this(null, null, (IReadOnlyCollection<IDescriptor>)items) { }

        public ParameterGroup([CanBeNull] string name, [NotNull] params IDescriptor[] items) 
            : this(name, null, (IReadOnlyCollection<IDescriptor>)items) { }

        public ParameterGroup([CanBeNull] string name, [CanBeNull] string description, [NotNull] params IDescriptor[] items)
            : this(name, description, (IReadOnlyCollection<IDescriptor>)items) { }

        public ParameterGroup([CanBeNull] string name, [CanBeNull] string description, [NotNull] IReadOnlyCollection<IDescriptor> items)
        {
            Name = name;
            Description = description;
            Items = items ?? throw new ArgumentNullException(nameof(items));
        }

        public string Name { get; }

        public string Description { get; }

        public IReadOnlyCollection<IDescriptor> Items { get; }

        public int Count => Items.Count;

        public bool IsEmpty => Count <= 0;

        public IEnumerator<IDescriptor> GetEnumerator() => Items.GetEnumerator();

        public override bool Equals(object obj)
        {
            if (null == obj) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is ParameterGroup other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ Items.GetHashCode();
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        private bool Equals(IGroupDescriptor other) => string.Equals(Name, other.Name) && Equals(Items, other.Items);

    }

    public sealed class ParameterGroupCollection : IReadOnlyCollection<IGroupDescriptor>, IReadOnlyCollection<IDescriptor>
    {

        private readonly LinkedList<IGroupDescriptor> _groups = new LinkedList<IGroupDescriptor>();

        public int Count => _groups.Count;

        public IEnumerator<IGroupDescriptor> GetEnumerator() => _groups.GetEnumerator();

        public ParameterGroupCollection Add([NotNull] params IDescriptor[] descriptors) => Add(null, descriptors);

        public ParameterGroupCollection Add([CanBeNull] string groupName, [NotNull] params IDescriptor[] descriptors) => Add(groupName, null, descriptors);

        public ParameterGroupCollection Add([CanBeNull] string groupName, [CanBeNull] string groupDescription, [NotNull] params IDescriptor[] descriptors)
        {
            _groups.AddLast(new ParameterGroup(groupName, groupDescription, descriptors));
            return this;
        }

        IEnumerator<IDescriptor> IEnumerable<IDescriptor>.GetEnumerator() => GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    }

    public abstract class RoutedParameter : IRoutedParameter
    {

        protected RoutedParameter(IParameterDescriptor parameter) => OriginalParameter = parameter ?? throw new ArgumentNullException(nameof(parameter));

        public IParameterDescriptor OriginalParameter { get; }

        public abstract Type ValueType { get; }

        public abstract string Key { get; }

        public abstract string Name { get; }

        public abstract string Unit { get; }

        public abstract string Description { get; }

        public abstract bool IsNullable { get; }

        public abstract object DefaultValue { get; }

        public abstract IEnumerable SelectableValues { get; }

        public abstract IReadonlyContext Metadata { get; }

        public abstract bool IsValid(object value);

        public sealed override int GetHashCode() => OriginalParameter.GetHashCode();

        public sealed override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (!(obj is IParameterDescriptor that)) return false;
            var rawThis = this.GetOriginalParameter();
            var rawThat = that.GetOriginalParameter();
            return Equals(rawThis, rawThat);
        }

    }

    public sealed class InformationRewrittenParameter : RoutedParameter
    {

        public InformationRewrittenParameter(IParameterDescriptor parameter, string name, string unit = null, string description = null) : base(parameter)
        {
            Name = name ?? parameter.Name;
            Unit = unit ?? parameter.Unit;
            Description = description ?? parameter.Description;
        }

        public override string Key => OriginalParameter.Key;

        public override string Name { get; }

        public override string Unit { get; }

        public override string Description { get; }

        public override Type ValueType => OriginalParameter.ValueType;

        public override bool IsNullable => OriginalParameter.IsNullable;

        public override object DefaultValue => OriginalParameter.DefaultValue;

        public override IEnumerable SelectableValues => OriginalParameter.SelectableValues;

        public override IReadonlyContext Metadata => OriginalParameter.Metadata;

        public override bool IsValid(object value) => OriginalParameter.IsValid(value);

    }

    public sealed class Parameter<T> : ContextProperty<T>, IParameterDescriptor
    {

        public sealed class Builder
        {

            public sealed class MetadataBuilder
            {

                public readonly IContext Context;

                public MetadataBuilder(IContext context) => Context = context;

                public MetadataBuilder SetRawProperty(IContextProperty property, object value)
                {
                    Context.Set(property, value);
                    return this;
                }

                public MetadataBuilder SetProperty<TP>(ContextProperty<TP> property, TP value)
                {
                    property.Set(Context, value);
                    return this;
                }

            }

            public string Key;

            public string Name;

            public string Unit;

            public string Description;

            public bool Nullable = typeof(T).IsNullableType();

            public Supplier<T> DefaultValueSupplier;

            public Supplier<IEnumerable<T>> SelectableValuesSupplier;

            public Predicate<T> Validator;

            public IReadonlyContext Metadata;

            public Builder(string name) : this(ParameterUtils.GenerateKeyByName(name), name) { }

            public Builder(string key, string name)
            {
                Key = key;
                Name = name;
            }

            public Builder SetKey(string key)
            {
                Key = key;
                return this;
            }

            public Builder SetName(string name)
            {
                Name = name;
                return this;
            }

            public Builder SetUnit(string unit)
            {
                Unit = unit;
                return this;
            }

            public Builder SetDescription(string description)
            {
                Description = description;
                return this;
            }

            public Builder SetNullable(bool nullable)
            {
                Nullable = nullable;
                return this;
            }

            public Builder SetDefaultValue(T defaultValue)
            {
                DefaultValueSupplier = () => defaultValue;
                return this;
            }

            public Builder SetDefaultValue(Supplier<T> defaultValueSupplier)
            {
                DefaultValueSupplier = defaultValueSupplier;
                return this;
            }

            [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
            public Builder SetSelectableValues(IEnumerable<T> selectableValues, bool setFirstAsDefault = false)
            {
                SelectableValuesSupplier = selectableValues == null ? (Supplier<IEnumerable<T>>)null : () => selectableValues;
                if (setFirstAsDefault) SetDefaultValue(selectableValues == null ? default : selectableValues.FirstOrDefault());
                return this;
            }

            [SuppressMessage("ReSharper", "PossibleMultipleEnumeration")]
            public Builder SetSelectableValues(Supplier<IEnumerable<T>> selectableValuesSupplier, bool setFirstAsDefault = false)
            {
                SelectableValuesSupplier = selectableValuesSupplier;
                if (setFirstAsDefault)
                {
                    if (selectableValuesSupplier == null)
                        SetDefaultValue(() => default);
                    else
                        SetDefaultValue(() => selectableValuesSupplier().FirstOrDefault());
                }
                return this;
            }

            public Builder SetValidator(Predicate<T> validator)
            {
                Validator = validator;
                return this;
            }

            public Builder SetMetadata(IReadonlyContext metadata)
            {
                Metadata = metadata;
                return this;
            }

            public Builder SetRawMetadata(IContextProperty property, object value)
            {
                SetMetadata(builder => builder.SetRawProperty(property, value));
                return this;
            }

            public Builder SetMetadata<TP>(ContextProperty<TP> property, TP value)
            {
                SetMetadata(builder => builder.SetProperty(property, value));
                return this;
            }

            public Builder SetMetadata(Action<MetadataBuilder> action)
            {
                var metaBuilder = new MetadataBuilder(Metadata?.CastOrConvertToSubType(roCtx => new Context(roCtx)) ?? new Context());
                action(metaBuilder);
                Metadata = metaBuilder.Context;
                return this;
            }

            public Builder Config(Action<Builder> action)
            {
                action(this);
                return this;
            }

            public Parameter<T> Build() => new Parameter<T>(this);

        }

        public Parameter(string name, T defaultValue = default, IEnumerable<T> selectableValues = null)
            : this(ParameterUtils.GenerateKeyByName(name), name, null, null, defaultValue, selectableValues) { }

        public Parameter(string name, string description, T defaultValue = default, IEnumerable<T> selectableValues = null)
            : this(ParameterUtils.GenerateKeyByName(name), name, null, description, defaultValue, selectableValues) { }

        public Parameter(string name, string unit, string description, T defaultValue = default, IEnumerable<T> selectableValues = null)
            : this(ParameterUtils.GenerateKeyByName(name), name, unit, description, defaultValue, selectableValues) { }

        public Parameter(string key, string name, string unit, string description, T defaultValue = default, IEnumerable<T> selectableValues = null)
            : this(CreateBuilderWithKey(key, name, defaultValue).SetUnit(unit).SetDescription(description).SetSelectableValues(selectableValues, false)) { }

        public Parameter(string name, Predicate<T> validator, T defaultValue = default)
            : this(ParameterUtils.GenerateKeyByName(name), name, null, null, validator, defaultValue) { }

        public Parameter(string name, string unit, string description, Predicate<T> validator, T defaultValue = default)
            : this(ParameterUtils.GenerateKeyByName(name), name, unit, description, validator, defaultValue) { }

        public Parameter(string key, string name, string unit, string description, Predicate<T> validator, T defaultValue = default)
            : this(CreateBuilderWithKey(key, name, defaultValue).SetUnit(unit).SetDescription(description).SetValidator(validator)) { }

        private Parameter(Builder builder)
        {
            Key = builder.Key ?? throw new ArgumentNullException(nameof(builder.Key));
            Name = builder.Name;
            Unit = builder.Unit;
            Description = builder.Description;
            IsNullable = builder.Nullable;
            DefaultValueSupplier = builder.DefaultValueSupplier;
            SelectableValuesSupplier = builder.SelectableValuesSupplier;
            Validator = builder.Validator;
            Metadata = builder.Metadata ?? EmptyContext.Instance;
        }

        public static Builder CreateBuilder(string name, T defaultValue = default) => new Builder(name).SetDefaultValue(defaultValue);

        public static Builder CreateBuilderWithKey(string key, string name, T defaultValue = default) => new Builder(key, name).SetDefaultValue(defaultValue);

        public static Parameter<T> OfEnum(string name, T defaultValue) => OfEnum(ParameterUtils.GenerateKeyByName(name), name, null, null, defaultValue);

        public static Parameter<T> OfEnum(string name, string unit = null, string description = null) => OfEnum(ParameterUtils.GenerateKeyByName(name), name, unit, description);

        public static Parameter<T> OfEnum(string key, string name, string unit, string description)
        {
            var values = EnumUtils.GetEnumValues<T>();
            if (values.Length == 0)
                throw new ArgumentException("enum type has no values");
            return new Parameter<T>(key, name, unit, description, values[0], values);
        }

        public static Parameter<T> OfEnum(string key, string name, string unit, string description, T defaultValue)
        {
            var values = EnumUtils.GetEnumValues<T>();
            if (values.Length == 0)
                throw new ArgumentException("enum type has no values");
            return new Parameter<T>(key, name, unit, description, defaultValue, values);
        }

        public string Key { get; }

        public string Name { get; }

        public string Unit { get; }

        public string Description { get; }

        public bool IsNullable { get; }

        public override bool HasDefaultValue => true;

        public override T DefaultValue => DefaultValueSupplier();

        public IEnumerable<T> SelectableValues => SelectableValuesSupplier?.Invoke();

        public Predicate<T> Validator { get; }

        public IReadonlyContext Metadata { get; }

        [NotNull] private Supplier<T> DefaultValueSupplier { get; }

        [CanBeNull] private Supplier<IEnumerable<T>> SelectableValuesSupplier { get; }

        public bool IsValid(object val)
        {
            if (IsNullable && val == null || val is T)
                return Validator?.Invoke((T) val) ?? true;
            return false;
        }

        public TOut Get<TOut>(IReadonlyContext context, Func<T, TOut> mappingFunc) => mappingFunc(Get(context));

        public override string ToString() => Key;

        object IParameterDescriptor.DefaultValue => DefaultValue;

        IEnumerable IParameterDescriptor.SelectableValues => SelectableValues;

    }

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

    public static class ParameterDescriptorExt
    {

        public static IParameterDescriptor GetOriginalParameter(this IParameterDescriptor parameter, bool recursively = true)
        {
            var param = parameter;
            while (param is IRoutedParameter routedParameter)
            {
                param = routedParameter.OriginalParameter;
                if (!recursively) return param;
            }
            return param;
        }

        public static object IsValidOrThrow(this IParameterDescriptor parameter, object value)
        {
            if(!parameter.IsValid(value)) throw new ArgumentException($"Value is invalid, parameter: {parameter.Name}, value: {value}");
            return value;
        }

        public static bool IsSelectable(this IParameterDescriptor parameter) => parameter.SelectableValues != null;

        public static bool IsMultiValue(this IParameterDescriptor parameter) => parameter.ValueType.IsArray && parameter.ValueType.GetArrayRank() == 1;

    }

    public static class ParameterGroupExt
    {

        public static IReadOnlyCollection<IParameterDescriptor> GetParameters(this IGroupDescriptor group) => group.Items.OfType<IParameterDescriptor>().ToList();

        public static IReadOnlyCollection<IGroupDescriptor> GetGroups(this IGroupDescriptor group) => group.Items.OfType<IGroupDescriptor>().ToList();

        public static IEnumerable<IParameterDescriptor> GetAllParameters(this IEnumerable<IDescriptor> descriptors) => descriptors.SelectMany(GetAllParameters);

        public static IEnumerable<IParameterDescriptor> GetAllParameters(this IDescriptor descriptor)
        {
            switch (descriptor)
            {
                case IParameterDescriptor parameter:
                    yield return parameter;
                    break;
                case IGroupDescriptor group:
                {
                    foreach (var child in group.Items)
                    foreach (var p in GetAllParameters(child))
                        yield return p;
                    break;
                }
            }
        }

        public static IEnumerable<IGroupDescriptor> GetAllGroups(this IEnumerable<IDescriptor> descriptors) =>
            descriptors.SelectMany(descriptor => GetAllGroups(descriptor, true));

        public static IEnumerable<IGroupDescriptor> GetAllGroups(this IDescriptor descriptor, bool includeSelf = true)
        {
            if (descriptor is IGroupDescriptor group)
            {
                if (includeSelf) yield return group;
                foreach (var child in GetGroups(group))
                foreach (var childGroup in GetAllGroups(child, false))
                    yield return childGroup;
            }
        }

    }

    public static class ParameterBuilderExt
    {

        public static Parameter<T>.Builder SetSelectablesForEnum<T>(this Parameter<T>.Builder builder, bool setFirstAsDefault = false) where T : Enum
        {
            var values = EnumUtils.GetEnumValues<T>();
            if (values.Length == 0)
                throw new ArgumentException("enum type has no values");
            return builder.SetSelectableValues(values, setFirstAsDefault);
        }

    }

    public static class ParameterUtils
    {

        public static string GenerateKeyByName(string paramName)
        {
            var chars = paramName
                .Filter(c => (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                .ToArray();
            if (chars.IsEmpty()) throw new ArgumentException($"The generated key of parameter is empty for name: '{paramName}'");
            chars[0] = char.ToLower(chars[0]);
            return new string(chars);
        }

    }

}
