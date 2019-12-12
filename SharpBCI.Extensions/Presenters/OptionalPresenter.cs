using System;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using MarukoLib.Lang;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    public class OptionalPresenter : IPresenter
    {

        private class Adapter : IPresentedParameterAdapter
        {

            private readonly IParameterDescriptor _parameter;

            private readonly Grid _container;

            private readonly CheckBox _checkBox;

            private readonly PresentedParameter _presented;

            private readonly ConstructorInfo _constructor;

            private readonly PropertyInfo _hasValueProperty, _valueProperty;

            public Adapter(IParameterDescriptor parameter, Type valueType, Grid container, CheckBox checkBox, PresentedParameter presented)
            {
                _parameter = parameter;
                _container = container;
                _checkBox = checkBox;
                _presented = presented;
                _constructor = _parameter.ValueType.GetConstructor(new [] {typeof(bool), valueType}) ?? throw new Exception("cannot found optional constructor");
                _hasValueProperty = _parameter.ValueType.GetProperty(nameof(Optional<object>.HasValue)) ?? throw new Exception("cannot found 'HasValue' property");
                _valueProperty = _parameter.ValueType.GetProperty(nameof(Optional<object>.Value)) ?? throw new Exception("cannot found 'Value' property");
            }

            public object GetValue() => _parameter.IsValidOrThrow(_constructor.Invoke(new[] { _checkBox.IsChecked ?? false, _presented.GetValue() }));

            public void SetValue(object value)
            {
                if (_parameter.ValueType.IsInstanceOfType(value))
                {
                    _checkBox.IsChecked = _hasValueProperty.GetValue(value) as bool?;
                    _presented.SetValue(_valueProperty.GetValue(value));
                }
            }

            public void SetEnabled(bool value) => _container.IsEnabled = value;

            public void SetValid(bool value) { }

        }

        public static readonly NamedProperty<object> CheckBoxContentProperty = new NamedProperty<object>("CheckBoxContent");

        public static readonly NamedProperty<IReadonlyContext> ValueTypePresentingContextProperty = new NamedProperty<IReadonlyContext>("ValueTypePresentingContext", EmptyContext.Instance);

        public static readonly OptionalPresenter Instance = new OptionalPresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var valueType = param.ValueType.GetGenericType(typeof(Optional<>));
            var valueTypeContext = ValueTypePresentingContextProperty.Get(param.Metadata);
            var valueTypeParam = new TypeOverridenParameter(param, valueType, valueTypeContext);
            var presented = valueTypeParam.GetPresenter().Present(valueTypeParam, updateCallback);

            var container = new Grid();
            container.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
            container.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.MinorSpacingGridLength});
            container.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.Star1GridLength});
            var checkbox = new CheckBox {IsChecked = true, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center};
            if (CheckBoxContentProperty.TryGet(param.Metadata, out var checkBoxContent)) checkbox.Content = checkBoxContent;

            void IsCheckedChangedEventHandler(object sender, RoutedEventArgs e)
            {
                presented.Element.IsEnabled = ((CheckBox) sender).IsChecked ?? false;
                updateCallback();
            }
            checkbox.Checked += IsCheckedChangedEventHandler;
            checkbox.Unchecked += IsCheckedChangedEventHandler;

            container.Children.Add(checkbox);
            Grid.SetColumn(checkbox, 0);
            container.Children.Add(presented.Element);
            Grid.SetColumn(presented.Element, 2);
            return new PresentedParameter(param, container, new Adapter(param, valueType, container, checkbox, presented));
        }

    }
}