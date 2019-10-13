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

        private class ComboBoxAccessor : IPresentedParameterAccessor
        {

            private readonly IParameterDescriptor _parameter;

            private readonly ComboBox _comboBox;

            public ComboBoxAccessor(IParameterDescriptor parameter, ComboBox comboBox)
            {
                _parameter = parameter;
                _comboBox = comboBox;
            }

            public object GetValue() => _parameter.IsValidOrThrow((_comboBox.SelectedValue as FrameworkElement)?.Tag as MarkerDefinition?);

            public void SetValue(object value)
            {
                _comboBox.FindAndSelectFirstByTag(value as MarkerDefinition?, 0);
                _comboBox.UpdateLayout();
            }
        }

        public static readonly NamedProperty<string> NullPlaceholderTextProperty = new NamedProperty<string>("NullPlaceholderText", "NULL");

        public static readonly NamedProperty<IEnumerable<MarkerDefinition>> MarkerDefinitionsProperty = new NamedProperty<IEnumerable<MarkerDefinition>>("MarkerDefinitions");

        public static readonly NamedProperty<string> MarkerPrefixFilterProperty = new NamedProperty<string>("MarkerPrefixFilter");

        public static readonly NamedProperty<Regex> MarkerRegexFilterProperty = new NamedProperty<Regex>("MarkerRegexFilter");

        public static readonly MarkerDefinitionPresenter Instance = new MarkerDefinitionPresenter();

        public static ContextProperty<Func<IParameterDescriptor, IEnumerable>> SelectableValuesFuncProperty = 
            new ContextProperty<Func<IParameterDescriptor, IEnumerable>>();

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
            foreach (var markerDefinition in markerDefinitions)
            {
                if (!brushCache.TryGetValue(markerDefinition.Color, out var brush)) 
                    brushCache[markerDefinition.Color] = brush = new SolidColorBrush(markerDefinition.Color.ToSwmColor());
                var itemContainer = new Grid {Tag = markerDefinition};
                itemContainer.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
                itemContainer.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.Star1GridLength});
                itemContainer.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
                var namespaceTextBlock = new TextBlock
                {
                    Text = markerDefinition.Namespace,
                    Foreground = Brushes.DarkSlateGray,
                    FontStyle = FontStyles.Italic
                };
                var nameTextBlock = new TextBlock
                {
                    Margin = new Thickness { Left = 5},
                    Text = markerDefinition.Name,
                    Foreground = brush,
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
                SelectedIndex = -1
            };
            comboBox.SizeChanged += (s, _) => ResizeBoxItem((ComboBox) s);
            comboBox.SelectionChanged += (s, _) => updateCallback();
            return new PresentedParameter(param, comboBox, new ComboBoxAccessor(param, comboBox), comboBox);
        }

    }
}