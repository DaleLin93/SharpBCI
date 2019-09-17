using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions.Data;

namespace SharpBCI.Extensions.Presenters
{

    public enum ParameterStateType
    {
        Valid, Enabled
    }

    public sealed class PresentedParameter
    {

        public delegate object ValueGetter();

        public delegate void ValueSetter(object value);

        public delegate bool ValueValidator(object value);

        public delegate void StateUpdater(ParameterStateType stateType, bool value);

        public sealed class ParamDelegates
        {

            [NotNull] public readonly ValueGetter Getter;

            [NotNull] public readonly ValueSetter Setter;

            [CanBeNull] public readonly ValueValidator Validator;

            [CanBeNull] public readonly StateUpdater Updater;

            public ParamDelegates([NotNull] ValueGetter getter, [NotNull] ValueSetter setter, 
                [CanBeNull] ValueValidator validator = null, [CanBeNull] StateUpdater updater = null)
            {
                Getter = getter ?? throw new ArgumentNullException(nameof(getter));
                Setter = setter ?? throw new ArgumentNullException(nameof(setter));
                Validator = validator;
                Updater = updater;
            }

        }

        [NotNull] public readonly IParameterDescriptor ParameterDescriptor;

        [NotNull] public readonly UIElement Element;

        [NotNull] public readonly ParamDelegates Delegates;

        public PresentedParameter([NotNull] IParameterDescriptor parameterDescriptor, [NotNull] UIElement element,
            [NotNull] ValueGetter getter, [NotNull] ValueSetter setter, [CanBeNull] ValueValidator validator = null, [CanBeNull] StateUpdater updater = null) 
            : this(parameterDescriptor, element, new ParamDelegates(getter, setter, validator, updater)) { }

        public PresentedParameter([NotNull] IParameterDescriptor parameterDescriptor, [NotNull] UIElement element, [NotNull] ParamDelegates delegates)
        {
            ParameterDescriptor = parameterDescriptor ?? throw new ArgumentNullException(nameof(parameterDescriptor));
            Element = element ?? throw new ArgumentNullException(nameof(element));
            Delegates = delegates ?? throw new ArgumentNullException(nameof(delegates));
        }

    }

    public interface IPresenter
    {

        [NotNull] PresentedParameter Present([NotNull] Window window, [NotNull] IParameterDescriptor parameter, [NotNull] Action updateCallback);

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class PresenterAttribute : Attribute
    {

        private static readonly IDictionary<Type, IPresenter> Presenters = new Dictionary<Type, IPresenter>();

        public PresenterAttribute(Type presenterType) => Presenter = Initiate(presenterType);

        private static IPresenter Initiate(Type factoryType)
        {
            IPresenter presenter;
            lock (Presenters)
                if (!Presenters.TryGetValue(factoryType, out presenter))
                {
                    if (!typeof(IPresenter).IsAssignableFrom(factoryType)) throw new ArgumentException("'presenterType' must implements IPresenter");
                    var constructor = factoryType.GetNoArgConstructor() ?? throw new ArgumentException("No-arg constructor must implements for IPresenter");
                    Presenters[factoryType] = presenter = (IPresenter)constructor.Invoke(EmptyArray<object>.Instance);
                }
            return presenter;
        }

        public IPresenter Presenter { get; }

    }

    public static class Presenters
    {

        public static ContextProperty<IPresenter> PresenterProperty = new ContextProperty<IPresenter>();

        public static ContextProperty<ITypeConverter> PresentTypeConverterProperty = new ContextProperty<ITypeConverter>();

        private static readonly IDictionary<Type, IPresenter> TypePresenters = new Dictionary<Type, IPresenter>
        {
            {typeof(bool), BooleanPresenter.Instance},
            {typeof(Color), ColorPresenter.Instance},
            {typeof(System.Drawing.Color), ColorPresenter.Instance},
            {typeof(Uri), UriPresenter.Instance},
            {typeof(Position1D), PositionPresenter.Instance},
            {typeof(PositionH1D), PositionPresenter.Instance},
            {typeof(PositionV1D), PositionPresenter.Instance},
            {typeof(Position2D), PositionPresenter.Instance},
        };

        public static bool TryGetPresentTypeConverter(this IParameterDescriptor parameter, out ITypeConverter converter) =>
            PresentTypeConverterProperty.TryGet(parameter.Metadata, out converter) && converter != null;

        [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
        public static IPresenter GetPresenter(this IParameterDescriptor param)
        {
            if (PresenterProperty.TryGet(param.Metadata, out var presenter)) return presenter;
            if (TryGetPresentTypeConverter(param, out _)) return TypeConvertedPresenter.Instance;
            if (param.IsSelectable()) return SelectablePresenter.Instance;
            if (param.IsMultiValue()) return MultiValuePresenter.Instance;
            return GetPresenter(param.ValueType) ?? throw new NotSupportedException($"presenter not found for type '{param.ValueType}'");
        }

        internal static IPresenter GetPresenter(this Type type)
        {
            if (type.IsNullableType(out var underlyingType))
                type = underlyingType;
            if (TypePresenters.TryGetValue(type, out var presenter))
                return presenter;
            if (typeof(IParameterizedObject).IsAssignableFrom(type))
                return ParameterizedObjectPresenter.Instance;
            return type.GetCustomAttribute<PresenterAttribute>()?.Presenter ?? PlainTextPresenter.Instance;
        }

    }

}
