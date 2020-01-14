using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    public class MarkerDefinitionPresenter : IPresenter
    {

        private class ComboBoxAdapter : IPresentedParameterAdapter
        {

            private readonly IParameterDescriptor _parameter;

            private readonly Type _actualValueType;

            private readonly ComboBox _comboBox;

            private readonly Action _updateAction;

            private readonly ReferenceCounter _textCallbackLock;

            private TextBox _textBox;

            private Border _textBoxBorder;

            private bool _isValid = true;

            public ComboBoxAdapter(IParameterDescriptor parameter, Type actualValueType, ComboBox comboBox, Action updateAction)
            {
                _parameter = parameter;
                _actualValueType = actualValueType;
                _comboBox = comboBox;
                _updateAction = updateAction;
                if (comboBox.IsEditable)
                {
                    _textCallbackLock = new ReferenceCounter();
                    comboBox.Loaded += ComboBox_OnLoaded;
                    comboBox.AddHandler(System.Windows.Controls.Primitives.TextBoxBase.TextChangedEvent, new TextChangedEventHandler(ComboBoxTextBox_TextChanged));
                }
                else
                {
                    _textCallbackLock = null;
                    comboBox.SizeChanged += ComboBox_OnSizeChanged;
                }
                comboBox.SelectionChanged += ComboBoxComboBoxOnSelectionChanged;
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
                    object value;
                    if (_comboBox.IsEditable)
                        value = string.IsNullOrEmpty(_comboBox.Text) ? (int?)null : int.Parse(_comboBox.Text);
                    else
                    {
                        var markerDef = (_comboBox.SelectedValue as FrameworkElement)?.Tag as MarkerDefinition?;
                        if (_actualValueType == typeof(int))
                            value = markerDef?.Code;
                        else if (_actualValueType == typeof(MarkerDefinition))
                            value = markerDef;
                        else
                            throw new NotSupportedException(_actualValueType.FullName);
                    }
                    return _parameter.IsValidOrThrow(value);
                }
                set
                {
                    if (_comboBox.IsEditable)
                        switch (value)
                        {
                            case int code:
                                _comboBox.Text = code.ToString();
                                break;
                            case MarkerDefinition markerDefinition:
                                _comboBox.Text = markerDefinition.Code.ToString();
                                break;
                            default:
                                _comboBox.Text = "";
                                break;
                        }
                    else
                    {
                        var success = false;
                        switch (value)
                        {
                            case int code:
                                success = _comboBox.FindAndSelectFirst(item =>
                                {
                                    if (!(item is FrameworkElement el) || !(el.Tag is MarkerDefinition def)) return false;
                                    return def.Code == code;
                                });
                                break;
                            case MarkerDefinition markerDefinition:
                                success = _comboBox.FindAndSelectFirstByTag(markerDefinition);
                                break;
                        }
                        if (!success)
                            _comboBox.SelectedIndex = 0;
                        _comboBox.UpdateLayout();
                    }
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

            private static void ComboBox_OnSizeChanged(object sender, SizeChangedEventArgs args) => ResizeBoxItem((ComboBox)sender);

            private void ComboBoxComboBoxOnSelectionChanged(object sender, SelectionChangedEventArgs args)
            {
                if (_textBox != null)
                {
                    var comboBox = (ComboBox) sender;
                    if (comboBox.SelectedItem != null && !(comboBox.SelectedItem is MarkerDefinitionItem))
                        using (_textCallbackLock.Ref())
                            _textBox.Text = "";
                }
                _updateAction();
            }

            private void ComboBoxTextBox_TextChanged(object sender, TextChangedEventArgs args)
            {
                if (!_textCallbackLock.IsReferred)
                    _updateAction();
            }

        }

        private class MarkerDefinitionItem : Grid
        {

            public readonly MarkerDefinition Marker;

            public MarkerDefinitionItem(MarkerDefinition marker) => Tag = Marker = marker;

            public override string ToString() => $"{Marker.Code}";

        }

        public static readonly NamedProperty<bool> CustomizeMarkerCodeProperty = new NamedProperty<bool>("CustomizeMarkerCode", false);

        public static readonly NamedProperty<string> NullPlaceholderTextProperty = new NamedProperty<string>("NullPlaceholderText", "NULL");

        public static readonly NamedProperty<IEnumerable<MarkerDefinition>> MarkerDefinitionsProperty = new NamedProperty<IEnumerable<MarkerDefinition>>("MarkerDefinitions");

        public static readonly NamedProperty<string> MarkerPrefixFilterProperty = new NamedProperty<string>("MarkerPrefixFilter");

        public static readonly NamedProperty<Regex> MarkerRegexFilterProperty = new NamedProperty<Regex>("MarkerRegexFilter");

        public static readonly MarkerDefinitionPresenter Instance = new MarkerDefinitionPresenter();

        public static ContextProperty<Func<IParameterDescriptor, IEnumerable>> SelectableValuesFuncProperty = 
            new ContextProperty<Func<IParameterDescriptor, IEnumerable>>();

        private static readonly ISet<Type> SupportedTypes = new HashSet<Type>(new[] {typeof(int), typeof(MarkerDefinition)});

        private static readonly Type CustomizeMarkerCodeSupportedType = typeof(int);

        private static void ResizeBoxItem(ItemsControl itemsControl)
        {
            var width = itemsControl.ActualWidth - itemsControl.Padding.Left - itemsControl.Padding.Right - 10 /* Assumed toggle button width */;
            foreach (var item in itemsControl.ItemsSource)
                if (item is FrameworkElement element)
                    element.Width = width;
        }

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            /* Read values */
            var valueType = param.ValueType;
            var actualType = valueType.IsNullableType(out var underlyingType) ? underlyingType : valueType;
            if (!SupportedTypes.Contains(actualType)) throw new NotSupportedException(actualType.FullName);
            var customizeMarkerCode = CustomizeMarkerCodeProperty.Get(param.Metadata);
            if (customizeMarkerCode && CustomizeMarkerCodeSupportedType != actualType) throw new NotSupportedException(actualType.FullName);
            var allowsNull = param.IsNullable;
            var markerDefinitions = MarkerDefinitionsProperty.TryGet(param.Metadata, out var propValue) 
                ? propValue : MarkerDefinitions.MarkerRegistry.Registered;
            if (MarkerPrefixFilterProperty.TryGet(param.Metadata, out var markerPrefixFilter) && !string.IsNullOrWhiteSpace(markerPrefixFilter))
                markerDefinitions = markerDefinitions.Where(md => md.Name.StartsWith(markerPrefixFilter));
            if (MarkerRegexFilterProperty.TryGet(param.Metadata, out var markerRegexFilter) && markerRegexFilter != null)
                markerDefinitions = markerDefinitions.Where(md => markerRegexFilter.IsMatch(md.Name));

            /* Generate combo box items */
            var comboBoxItems = new LinkedList<object>();
            if (allowsNull) comboBoxItems.AddLast(ViewHelper.CreateDefaultComboBoxItem(NullPlaceholderTextProperty.Get(param.Metadata), TextAlignment.Center));
            var brushCache = new Dictionary<uint, Brush>();
            Brush GetSolidColorBrush(uint color) => brushCache.GetOrCreate(color, c => new SolidColorBrush(c.ToSwmColor()));
            foreach (var markerDefinition in markerDefinitions)
            {
                Brush namespaceBrush, nameBrush;
                namespaceBrush = GetSolidColorBrush(MarkerDefinitions.NamespaceRegistry.LookUp(markerDefinition.Namespace, out var @namespace)
                    ? @namespace.Color : MarkerNamespaceDefinition.DefaultColor);
                nameBrush = brushCache.GetOrCreate(markerDefinition.Color, color => new SolidColorBrush(color.ToSwmColor()));

                var itemContainer = new MarkerDefinitionItem(markerDefinition);
                itemContainer.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
                itemContainer.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.Star1GridLength});
                itemContainer.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
                var namespaceTextBlock = new TextBlock
                {
                    Text = markerDefinition.Namespace,
                    Foreground = namespaceBrush,
                    FontStyle = FontStyles.Italic
                };
                var nameTextBlock = new TextBlock
                {
                    Margin = new Thickness { Left = 5},
                    Text = markerDefinition.Name,
                    Foreground = nameBrush,
                    FontWeight = FontWeights.Bold
                };
                var codeTextBlock = new TextBlock
                {
                    VerticalAlignment = VerticalAlignment.Center,
                    Text = $"#{markerDefinition.Code}",
                    Foreground = Brushes.SlateGray,
                    FontWeight = FontWeights.Light,
                    FontSize = 8
                };
                itemContainer.Children.Add(namespaceTextBlock);
                itemContainer.Children.Add(nameTextBlock);
                itemContainer.Children.Add(codeTextBlock);
                Grid.SetColumn(namespaceTextBlock, 0);
                Grid.SetColumn(nameTextBlock, 1); 
                Grid.SetColumn(codeTextBlock, 2); 
                comboBoxItems.AddLast(itemContainer);
            }
            var comboBox = new ComboBox
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Center,
                ItemsSource = comboBoxItems,
                SelectedIndex = -1,
                IsEditable = customizeMarkerCode
            };
            return new PresentedParameter(param, comboBox, new ComboBoxAdapter(param, actualType, comboBox, updateCallback));
        }

    }
}