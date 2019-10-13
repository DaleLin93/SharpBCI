using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.UI;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    public class SelectablePresenter : IPresenter
    {

        private class ComboBoxAccessor : IPresentedParameterAccessor
        {

            private readonly IParameterDescriptor _parameter;

            private readonly Func<object, string> _toStringFunc;

            private readonly ComboBox _comboBox;

            public ComboBoxAccessor(IParameterDescriptor parameter, Func<object, string> toStringFunc, ComboBox comboBox)
            {
                _parameter = parameter;
                _toStringFunc = toStringFunc;
                _comboBox = comboBox;
            }

            public object GetValue()
            {
                var value = ToStringOverridenWrapper.TryUnwrap(_comboBox.SelectedValue);
                if (ReferenceEquals(NullValue, value)) value = null;
                return _parameter.IsValidOrThrow(value);
            }

            public void SetValue(object value) => _comboBox.FindAndSelectFirstByString(_toStringFunc(value), 0);

        }

        private class RadioGroupAdapter : IPresentedParameterAdapter
        {

            private readonly IParameterDescriptor _parameter;

            private readonly Rectangle _rectangle;

            private readonly UIElement _container;

            private readonly IList<RadioButton> _radioButtons;

            public RadioGroupAdapter(IParameterDescriptor parameter, Rectangle rectangle, UIElement container, IList<RadioButton> radioButtons)
            {
                _parameter = parameter;
                _rectangle = rectangle;
                _container = container;
                _radioButtons = radioButtons;
            }

            public object GetValue()
            {
                var value = ToStringOverridenWrapper.TryUnwrap(_radioButtons.First(rb => rb.IsChecked ?? false).Content);
                if (ReferenceEquals(NullValue, value)) value = null;
                return _parameter.IsValidOrThrow(value);
            }

            public void SetValue(object value)
            {
                var @checked = false;
                foreach (var radioButton in _radioButtons)
                {
                    var equal = Equals(ToStringOverridenWrapper.TryUnwrap(radioButton.Content), value);
                    if (equal) @checked = true;
                    radioButton.IsChecked = equal;
                }
                if (!@checked && _radioButtons.Any()) _radioButtons[0].IsChecked = true;
            }

            public void SetEnabled(bool value) => _container.IsEnabled = value;

            public void SetValid(bool value) => _rectangle.Fill = value ? Brushes.Transparent : ViewConstants.InvalidColorBrush;

        }

        private static readonly string NullValue = "<NULL>";

        public static readonly NamedProperty<bool> UseRadioGroupProperty = new NamedProperty<bool>("UseRadioGroup", false);

        public static readonly NamedProperty<Orientation> RadioGroupOrientationProperty = new NamedProperty<Orientation>("RadioGroupOrientation", Orientation.Horizontal);

        public static readonly SelectablePresenter Instance = new SelectablePresenter();

        public static ContextProperty<Func<IParameterDescriptor, IEnumerable>> SelectableValuesFuncProperty = 
            new ContextProperty<Func<IParameterDescriptor, IEnumerable>>();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            IEnumerable items;
            if (SelectableValuesFuncProperty.TryGet(param.Metadata, out var selectableValuesFunc))
                items = selectableValuesFunc(param);
            else if (param.IsSelectable())
                items = param.SelectableValues;
            else if (param.ValueType.IsEnum)
                items = Enum.GetValues(param.ValueType);
            else if (param.ValueType.IsNullableType(out var underlyingType) && underlyingType.IsEnum)
                items = new object[] {NullValue}.Concat(Enum.GetValues(underlyingType).OfType<object>());
            else
                throw new ProgrammingException("Parameter.SelectableValues or SelectablePresenter.SelectableValuesFuncProperty must be assigned");
            return UseRadioGroupProperty.Get(param.Metadata)
                ? PresentRadioButtons(param, items, param.ConvertValueToString, updateCallback)
                : PresentComboBox(param, items, param.ConvertValueToString, updateCallback);
        }

        public PresentedParameter PresentComboBox(IParameterDescriptor param, IEnumerable items, Func<object, string> toStringFunc, Action updateCallback)
        {
            var comboBox = new ComboBox();
            comboBox.SelectionChanged += (sender, args) => updateCallback();
            comboBox.ItemsSource = ToStringOverridenWrapper.Of(items, toStringFunc);
            return new PresentedParameter(param, comboBox, new ComboBoxAccessor(param, toStringFunc, comboBox), comboBox);
        }

        public PresentedParameter PresentRadioButtons(IParameterDescriptor param, IEnumerable items, Func<object, string> toStringFunc, Action updateCallback)
        {
            var guid = Guid.NewGuid().ToString();
            var radioButtons = (from object item in items
                select new RadioButton
                {
                    GroupName = guid,
                    Content = ToStringOverridenWrapper.Wrap(item, toStringFunc),
                    Margin = new Thickness
                    {
                        Top = ViewConstants.MinorSpacing / 2.0,
                        Bottom = ViewConstants.MinorSpacing / 2.0,
                        Left = ViewConstants.MinorSpacing / 2.0,
                        Right = ViewConstants.MinorSpacing
                    }
                }).ToList();
            var grid = new Grid {Margin = new Thickness(0, ViewConstants.MinorSpacing / 2.0, 0, 0)};

            var bgRect = new Rectangle
            {
                RadiusX = 5, RadiusY = 5,
                Stroke = new SolidColorBrush(Color.FromScRgb(1, 0.2F, 0.2F, 0.2F)), 
                StrokeThickness = 1,
            };
            grid.Children.Add(bgRect);

            var container = RadioGroupOrientationProperty.Get(param.Metadata) == Orientation.Horizontal ? new WrapPanel() : (Panel)new StackPanel();
            container.Margin = new Thickness(ViewConstants.MinorSpacing);
            foreach (var radioButton in radioButtons)
            {
                container.Children.Add(radioButton);
                radioButton.Checked += (sender, e) => updateCallback();
            }
            grid.Children.Add(container);
            return new PresentedParameter(param, grid, new RadioGroupAdapter(param, bgRect, container, radioButtons));
        }

    }
}