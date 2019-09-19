using System;
using System.Windows;
using System.Windows.Controls;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Windows
{

    public static class ViewHelper
    {

        public const string SharedResourceUri = "pack://application:,,,/SharpBCI.Extensions;component/Resources/SharedResourceDictionary.xaml";

        public static readonly ResourceDictionary Resources = new ResourceDictionary {Source = new Uri(SharedResourceUri, UriKind.RelativeOrAbsolute)};

        public static object TryFindResource(string name)
        {
            var res = Resources[name];
            return res == DependencyProperty.UnsetValue ? null : res;
        }

        public static GroupHeader CreateGroupHeader(string header, string description) =>
            new GroupHeader
            {
                SeparatorStyle = TryFindResource("ParamGroupSeparator") as Style,
                HeaderTextStyle = TryFindResource("ParamGroupHeader") as Style,
                Header = header,
                Description = description
            };

        public static TextBlock CreateParamNameTextBlock(IParameterDescriptor param) => new TextBlock
        {
            Style = (Style)TryFindResource("LabelText"),
            Text = param.Name + (param.Unit == null ? "" : $" ({param.Unit})"),
            ToolTip = $"Key: {param.Key}\nValue Type: {param.ValueType.GetFriendlyName()}{(param.Description == null ? "" : "\n" + param.Description)}",
        };

        public static StackPanel AddGroupPanel(this Panel parent, string header, string description, int depth = 0)
        {
            var stackPanel = new StackPanel();
            if (depth > 0) stackPanel.Margin = new Thickness { Left = ViewConstants.Intend * depth };
            stackPanel.Children.Add(CreateGroupHeader(header, description));
            parent.Children.Add(stackPanel);
            return stackPanel;
        }

        public static Grid AddRow(this Panel parent, string label, UIElement rightPart, uint rowHeight = 0) =>
            AddRow(parent, label == null ? null : new TextBlock { Text = label, Style = TryFindResource("LabelText") as Style }, rightPart, rowHeight);

        public static Grid AddRow(this Panel parent, UIElement leftPart, UIElement rightPart, uint rowHeight = 0)
        {
            var row = new Grid { Margin = ViewConstants.RowMargin };
            if (rowHeight > 0) row.Height = rowHeight;
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star), MaxWidth = 300 });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(ViewConstants.MajorSpacing) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2.5, GridUnitType.Star) });
            row.Children.Add(leftPart);
            Grid.SetColumn(leftPart, 0);
            row.Children.Add(rightPart);
            Grid.SetColumn(rightPart, 2);
            parent.Children.Add(row);
            return row; 
        }

    }

}
