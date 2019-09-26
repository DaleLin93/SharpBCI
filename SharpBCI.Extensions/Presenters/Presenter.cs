using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    public interface IPresentedParameterAccessor
    {

        object GetValue();

        void SetValue(object value);

    }

    public interface IPresentedParameterStateHandler
    {

        void SetEnabled(bool value);

        void SetValid(bool value);

    }

    public interface IPresentedParameterAdapter : IPresentedParameterAccessor, IPresentedParameterStateHandler { }

    public sealed class PresentedParameter : IPresentedParameterAdapter
    {

        private class SimpleStateHandler : IPresentedParameterStateHandler
        {

            private readonly Control _control;

            public SimpleStateHandler(Control control) => _control = control;

            public void SetEnabled(bool value) => _control.IsEnabled = value;

            public void SetValid(bool value) => _control.Background = value ? Brushes.Transparent : ViewConstants.InvalidColorBrush;

        }

        [NotNull] public readonly IParameterDescriptor ParameterDescriptor;

        [NotNull] public readonly UIElement Element;

        [NotNull] private readonly IPresentedParameterAccessor _accessor;

        [CanBeNull] private readonly IPresentedParameterStateHandler _stateHandler;

        private bool _isEnabled = true;

        private bool _isValid = true;

        public PresentedParameter([NotNull] IParameterDescriptor parameterDescriptor, [NotNull] UIElement element, [NotNull] IPresentedParameterAdapter adapter) 
            : this(parameterDescriptor, element, adapter, adapter) { }

        public PresentedParameter([NotNull] IParameterDescriptor parameterDescriptor, [NotNull] UIElement element,
            [NotNull] IPresentedParameterAccessor accessor, [CanBeNull] Control control = null) 
            : this(parameterDescriptor, element, accessor, control == null ? null : new SimpleStateHandler(control)) { }

        public PresentedParameter([NotNull] IParameterDescriptor parameterDescriptor, [NotNull] UIElement element,
            [NotNull] IPresentedParameterAccessor accessor, [CanBeNull] IPresentedParameterStateHandler handler = null)
        {
            ParameterDescriptor = parameterDescriptor ?? throw new ArgumentNullException(nameof(parameterDescriptor));
            Element = element ?? throw new ArgumentNullException(nameof(element));
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _stateHandler = handler;
        }

        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                _stateHandler?.SetEnabled(_isEnabled);
            }
        }

        public bool IsValid
        {
            get => _isValid;
            set
            {
                if (_isValid == value) return;
                _isValid = value;
                _stateHandler?.SetValid(_isValid);
            }
        }
        
        public object GetValue()
        {
            object value;
            try
            {
                value = _accessor.GetValue();
                IsValid = true;
            }
            catch (Exception)
            {
                IsValid = false;
                throw;
            }
            return value;
        }

        public void SetValue(object value) => _accessor.SetValue(value);

        public void SetEnabled(bool value) => IsEnabled = value;

        public void SetValid(bool value) => IsValid = value;

    }

    public interface IPresenter
    {

        [NotNull] PresentedParameter Present([NotNull] IParameterDescriptor parameter, [NotNull] Action updateCallback);

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class PresenterAttribute : Attribute
    {

        private static readonly IDictionary<Type, IPresenter> Presenters = new Dictionary<Type, IPresenter>();

        public PresenterAttribute(Type presenterType) => Presenter = Initiate(presenterType);

        private static IPresenter Initiate(Type presenterType)
        {
            IPresenter presenter;
            lock (Presenters)
                if (!Presenters.TryGetValue(presenterType, out presenter))
                {
                    if (!typeof(IPresenter).IsAssignableFrom(presenterType)) throw new ArgumentException("'presenterType' must implements IPresenter");
                    Presenters[presenterType] = presenter = (IPresenter) Activator.CreateInstance(presenterType);
                }
            return presenter;
        }

        public IPresenter Presenter { get; }

    }

    public static class Presenters
    {

        public const string NullPlaceholder = "{NULL}";

        private delegate IPresenter PresenterSelector(IParameterDescriptor parameter);

        public static ContextProperty<IPresenter> PresenterProperty = new ContextProperty<IPresenter>();

        public static ContextProperty<ITypeConverter> PresentTypeConverterProperty = new ContextProperty<ITypeConverter>();

        private static readonly IDictionary<Type, IPresenter> TypePresenters = new Dictionary<Type, IPresenter>
        {
            {typeof(bool), BooleanPresenter.Instance},
            {typeof(Color), ColorPresenter.Instance},
            {typeof(System.Drawing.Color), ColorPresenter.Instance},
            {typeof(DateTime), DateTimePresenter.Instance},
            {typeof(Uri), UriPresenter.Instance},
            {typeof(Position1D), PositionPresenter.Instance},
            {typeof(PositionH1D), PositionPresenter.Instance},
            {typeof(PositionV1D), PositionPresenter.Instance},
            {typeof(Position2D), PositionPresenter.Instance},
        };

        private static readonly IList<PresenterSelector> CommonPresenterSelectors = new List<PresenterSelector>
        {
            parameter => PresenterProperty.TryGet(parameter.Metadata, out var presenter) ? presenter : null,
            parameter => parameter.ValueType.GetCustomAttribute<PresenterAttribute>()?.Presenter,
            BoolPresenterSelector(NeedConvert, TypeConvertedPresenter.Instance),
            BoolPresenterSelector(ParameterDescriptorExt.IsSelectable, SelectablePresenter.Instance),
            BoolPresenterSelector(ParameterDescriptorExt.IsMultiValue, MultiValuePresenter.Instance),
            parameter => GetPresenter(parameter.ValueType)
        };

        public static bool NeedConvert(IParameterDescriptor param) => TryGetPresentTypeConverter(param, out _);

        public static bool TryGetPresentTypeConverter(this IParameterDescriptor parameter, out ITypeConverter converter) =>
            PresentTypeConverterProperty.TryGet(parameter.Metadata, out converter) && converter != null;

        [SuppressMessage("ReSharper", "LoopCanBeConvertedToQuery")]
        public static IPresenter GetPresenter(this IParameterDescriptor param)
        {
            IPresenter presenter;
            foreach (var presenterSelector in CommonPresenterSelectors)
                if ((presenter = presenterSelector(param)) != null)
                    return presenter;
            throw new NotSupportedException($"presenter not found for type '{param.ValueType}'");
        }

        public static object ParseValueFromString(this IParameterDescriptor parameter, string strVal)
        {
            if (Equals(NullPlaceholder, strVal)) return null;
            return TryGetPresentTypeConverter(parameter, out var converter)
                ? converter.ConvertBackward(ParseValueFromString(converter.OutputType, strVal))
                : ParseValueFromString(parameter.ValueType, strVal);
        }

        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        public static object ParseValueFromString(Type type, string strVal)
        {
            if (Equals(NullPlaceholder, strVal)) return null;
            if (type.IsArray)
                if (type.GetArrayRank() == 1)
                {
                    strVal = strVal.Trim();
                    var substrings = strVal.Split(' ').Where(str => !str.IsBlank()).ToArray();
                    var array = Array.CreateInstance(type.GetElementType(), substrings.Length);
                    for (var i = 0; i < substrings.Length; i++)
                        array.SetValue(ParseValueFromString(type.GetElementType(), substrings[i]), i);
                    return array;
                }
                else
                    throw new NotSupportedException("Only 1D-array was supported");

            if (type == typeof(string)) return strVal;

            var nullableType = type.IsNullableType(out var underlyingType);
            var actualType = nullableType ? underlyingType : type;

            if (actualType.IsEnum)
            {
                var enumValues = Enum.GetValues(actualType);
                foreach (var enumValue in enumValues)
                    if (Equals(enumValue.ToString(), strVal))
                        return enumValue;
                throw new ArgumentException($"{actualType.Name} value not found by name: '{strVal}'");
            }

            if (!actualType.IsPrimitive) throw new ArgumentException("type is not supported, type: " + type.FullName);

            if (strVal?.IsEmpty() ?? true)
                if (nullableType)
                    return null;
                else
                    throw new ArgumentException("cannot convert empty string to type: " + type.FullName);

            if (actualType == typeof(char)) return strVal[0];
            if (actualType == typeof(byte)) return byte.Parse(strVal);
            if (actualType == typeof(sbyte)) return sbyte.Parse(strVal);
            if (actualType == typeof(short)) return short.Parse(strVal);
            if (actualType == typeof(ushort)) return ushort.Parse(strVal);
            if (actualType == typeof(int)) return int.Parse(strVal);
            if (actualType == typeof(uint)) return uint.Parse(strVal);
            if (actualType == typeof(ulong)) return ulong.Parse(strVal);
            if (actualType == typeof(float)) return float.Parse(strVal);
            if (actualType == typeof(double)) return double.Parse(strVal);
            if (actualType == typeof(decimal)) return decimal.Parse(strVal);

            throw new Exception("unreachable statement");
        }

        public static string ConvertValueToString(this IParameterDescriptor parameter, object val)
        {
            if (TryGetPresentTypeConverter(parameter, out var converter)) val = converter.ConvertForward(val);
            return val == null ? NullPlaceholder : ConvertValueToString(val.GetType(), val);
        }

        public static string ConvertValueToString(this Type type, object value)
        {
            if (type.IsArray)
            {
                if (type.GetArrayRank() == 1 && (type.GetElementType()?.IsPrimitive ?? false))
                {
                    var stringBuilder = new StringBuilder();
                    var array = (Array)value;
                    for (var i = 1; i <= array.Length; i++)
                    {
                        stringBuilder.Append(array.GetValue(i - 1));
                        if (i != array.Length) stringBuilder.Append(' ');
                    }
                    return stringBuilder.ToString();
                }
                throw new NotSupportedException();
            }
            if (value is IDescribable describable) return describable.GetShortDescription();
            return value.ToString();
        }

        internal static IPresenter GetPresenter(this Type type)
        {
            if (type.IsNullableType(out var underlyingType)) type = underlyingType;
            if (TypePresenters.TryGetValue(type, out var presenter)) return presenter;
            if (typeof(IParameterizedObject).IsAssignableFrom(type)) return ParameterizedObjectPresenter.Instance;
            return PlainTextPresenter.Instance;
        }

        private static PresenterSelector BoolPresenterSelector(Predicate<IParameterDescriptor> predicate, IPresenter presenter) => parameter => predicate(parameter) ? presenter : null;

    }

}
