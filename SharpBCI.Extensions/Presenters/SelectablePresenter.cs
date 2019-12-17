using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.UI;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    public class SelectablePresenter : IPresenter
    {

        private class ComboBoxAdapter : IPresentedParameterAdapter
        {

            private readonly IParameterDescriptor _parameter;

            private readonly Func<object, string> _toStringFunc;

            private readonly ComboBox _comboBox;

            private readonly Action _updateAction;

            private readonly ReferenceCounter _textCallbackLock;

            private TextBox _textBox;

            private Border _textBoxBorder;

            private bool _isValid = true;

            public ComboBoxAdapter(IParameterDescriptor parameter, Func<object, string> toStringFunc, ComboBox comboBox, Action updateCallback)
            {
                _parameter = parameter;
                _toStringFunc = toStringFunc;
                _comboBox = comboBox;
                _updateAction = updateCallback;
                if (comboBox.IsEditable)
                {
                    _textCallbackLock = new ReferenceCounter();
                    comboBox.Loaded += ComboBox_OnLoaded;
                    comboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler(ComboBoxTextBox_TextChanged));
                }
                else
                    _textCallbackLock = null;
            }

            public bool IsEnabled
            {
                get => _comboBox.IsEnabled;
                set => _comboBox.IsEnabled = value;
            }

            public bool IsValid
            {
                get => _isValid;
                set
                {
                    if (_isValid == value) return;
                    _isValid = value;
                    var brush = value ? Brushes.Transparent : ViewConstants.InvalidColorBrush;
                    if (_comboBox.IsEditable)
                    {
                        if (_textBoxBorder != null)
                            _textBoxBorder.Background = brush;
                        else if (_textBox != null)
                            _textBox.Background = brush;
                    }
                    else
                        _comboBox.Background = brush;
                }
            }

            public object Value
            {
                get
                {
                    var value = _comboBox.IsEditable ? _comboBox.Text : ToStringOverridenWrapper.TryUnwrap(_comboBox.SelectedValue);
                    if (ReferenceEquals(NullValue, value)) value = null;
                    return _parameter.IsValidOrThrow(value);
                }
                set
                {
                    if (_comboBox.IsEditable)
                        _comboBox.Text = value.ToString();
                    else
                        _comboBox.FindAndSelectFirstByString(_toStringFunc(value), 0);
                }
            }

            private void ComboBox_OnLoaded(object sender, RoutedEventArgs args)
            {
                var comboBox = (ComboBox)sender;
                comboBox.Loaded -= ComboBox_OnLoaded;
                if ((_textBox = comboBox.Template.FindName("PART_EditableTextBox", comboBox) as TextBox) == null) return;
                _textBox.Background = Brushes.Transparent;
                if ((_textBoxBorder = _textBox.Parent as Border) == null) return;
                _textBoxBorder.Background = Brushes.Transparent;
            }

            private void ComboBoxTextBox_TextChanged(object sender, TextChangedEventArgs args)
            {
                if (!_textCallbackLock.IsReferred)
                    _updateAction?.Invoke();
            }

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

            public bool IsEnabled
            {
                get => _container.IsEnabled;
                set => _container.IsEnabled = value;
            }

            public bool IsValid
            {
                get => _rectangle.Fill != ViewConstants.InvalidColorBrush;
                set => _rectangle.Fill = value ? Brushes.Transparent : ViewConstants.InvalidColorBrush;
            }

            public object Value
            {
                get
                {
                    var value = ToStringOverridenWrapper.TryUnwrap(_radioButtons.First(rb => rb.IsChecked ?? false).Content);
                    if (ReferenceEquals(NullValue, value)) value = null;
                    return _parameter.IsValidOrThrow(value);
                }
                set
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
            }

        }

        private const string NullValue = "<NULL>";

        /// <summary>
        /// Add refresh button refresh selectable values at any time.
        /// Only supported for presentation style of combo box.
        /// Default value: <code>false</code>
        /// </summary>
        public static readonly NamedProperty<bool> RefreshableProperty = new NamedProperty<bool>("Refreshable", false);

        /// <summary>
        /// Make combo box editable to customize value.
        /// Only supported for presentation style of combo box and for string as value type (or string convertible).
        /// Default value: <code>false</code>
        /// </summary>
        public static readonly NamedProperty<bool> CustomizableProperty = new NamedProperty<bool>("Customizable", false);

        /// <summary>
        /// Use radio button group to select value instead of a combo box.
        /// Default value: <code>false</code>
        /// </summary>
        public static readonly NamedProperty<bool> UseRadioGroupProperty = new NamedProperty<bool>("UseRadioGroup", false);

        public static readonly NamedProperty<Orientation> RadioGroupOrientationProperty = 
            new NamedProperty<Orientation>("RadioGroupOrientation", Orientation.Horizontal);

        public static readonly NamedProperty<Func<IParameterDescriptor, IEnumerable>> SelectableValuesFuncProperty = 
            new NamedProperty<Func<IParameterDescriptor, IEnumerable>>("SelectableValuesFunc");

        public static readonly SelectablePresenter Instance = new SelectablePresenter();

        private static IEnumerable GetSelectableValues(IParameterDescriptor param)
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
            return items;
        }

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            return UseRadioGroupProperty.Get(param.Metadata)
                ? PresentRadioButtons(param, param.ConvertValueToString, updateCallback)
                : PresentComboBox(param, param.ConvertValueToString, updateCallback);
        }

        public PresentedParameter PresentComboBox(IParameterDescriptor param, Func<object, string> toStringFunc, Action updateCallback)
        {
            var refreshable = RefreshableProperty.Get(param.Metadata);
            var customizable = CustomizableProperty.Get(param.Metadata);
            if (customizable && param.ValueType != typeof(string)) throw new ProgrammingException("customizable feature is only supported for string type");
            var comboBox = new ComboBox();
            comboBox.SelectionChanged += (sender, args) => updateCallback();
            comboBox.ItemsSource = ToStringOverridenWrapper.Of(GetSelectableValues(param), toStringFunc);
            comboBox.IsEditable = customizable;
            var adapter = new ComboBoxAdapter(param, toStringFunc, comboBox, updateCallback);

            if (!refreshable) return new PresentedParameter(param, comboBox, adapter);

            /* Create 3 columns grid for 'ComboBox-Spacing-RefreshButton' */
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.Star1GridLength});
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.MinorSpacingGridLength});
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});

            /* Add ComboBox at 1st column */
            grid.Children.Add(comboBox);
            Grid.SetColumn(comboBox, 0);

            /* Add RefreshButton at 3rd column */
            var refreshValuesBtn = new Button
            {
                ToolTip = "Refresh Values",
                HorizontalAlignment = HorizontalAlignment.Right,
                Width = ViewConstants.DefaultRowHeight,
                Content = new Image {Margin = new Thickness(2), Source = new BitmapImage(new Uri(ViewConstants.ResetImageUri, UriKind.Absolute))}
            };
            refreshValuesBtn.Click += (sender, args) =>
            {
                var selected = comboBox.SelectedItem;
                comboBox.ItemsSource = ToStringOverridenWrapper.Of(GetSelectableValues(param), toStringFunc);
                if (selected != null) adapter.Value = selected;
                updateCallback();
            };
            grid.Children.Add(refreshValuesBtn);
            Grid.SetColumn(refreshValuesBtn, 2);

            return new PresentedParameter(param, grid, adapter);
        }

        public PresentedParameter PresentRadioButtons(IParameterDescriptor param, Func<object, string> toStringFunc, Action updateCallback)
        {
            if (RefreshableProperty.Get(param.Metadata)) throw new ProgrammingException("refreshable feature not supported for radio button group style");
            if (CustomizableProperty.Get(param.Metadata)) throw new ProgrammingException("customizable feature not supported for radio button group style");
            var guid = Guid.NewGuid().ToString();
            var radioButtons = (from object item in GetSelectableValues(param)
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