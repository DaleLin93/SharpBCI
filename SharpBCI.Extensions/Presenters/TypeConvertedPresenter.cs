using System;
using System.Collections;
using System.Linq;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Presenters
{

    public class TypeConvertedPresenter : IPresenter
    {

        private sealed class TypeConvertedParameter : RoutedParameter
        {

            public TypeConvertedParameter(IParameterDescriptor originalParameter, ITypeConverter typeConverter, IReadonlyContext metadata) : base(originalParameter)
            {
                TypeConverter = typeConverter;
                Metadata = metadata ?? EmptyContext.Instance;
            }

            public ITypeConverter TypeConverter { get; }

            public override string Key => OriginalParameter.Key;

            public override string Name => OriginalParameter.Name;

            public override string Unit => OriginalParameter.Unit;

            public override string Description => OriginalParameter.Description;

            public override Type ValueType => TypeConverter.OutputType;

            public override bool IsNullable => OriginalParameter.IsNullable;

            public override object DefaultValue => TypeConverter.ConvertForward(OriginalParameter.DefaultValue);

            public override IEnumerable SelectableValues => OriginalParameter.SelectableValues?
                .Cast<object>().Select(value => TypeConverter.ConvertForward(value));

            public override IReadonlyContext Metadata { get; }

            public override bool IsValid(object value) => OriginalParameter.IsValid(TypeConverter.ConvertBackward(value));

        }

        private class Adapter : IPresentedParameterAdapter
        {

            private readonly TypeConvertedParameter _parameter;

            private readonly PresentedParameter _presented;

            public Adapter(TypeConvertedParameter parameter, PresentedParameter presented)
            {
                _parameter = parameter;
                _presented = presented;
            }

            public bool IsEnabled
            {
                get => _presented.IsEnabled;
                set => _presented.IsEnabled = value;
            }

            public bool IsValid
            {
                get => _presented.IsValid;
                set => _presented.IsValid = value;
            }

            public object Value
            {
                get => _parameter.TypeConverter.ConvertBackward(_presented.Value);
                set => _presented.Value = _parameter.TypeConverter.ConvertForward(value);
            }

        }

        public static readonly NamedProperty<IReadonlyContext> ConvertedContextProperty = new NamedProperty<IReadonlyContext>("ConvertedContext", EmptyContext.Instance);

        public static readonly TypeConvertedPresenter Instance = new TypeConvertedPresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            if (!param.TryGetPresentTypeConverter(out var converter)) throw new ArgumentException();
            var converted = new TypeConvertedParameter(param, converter, ConvertedContextProperty.Get(param.Metadata));
            var presented = converted.GetPresenter().Present(converted, updateCallback);
            return new PresentedParameter(param, presented.Element, new Adapter(converted, presented));
        }

    }

}