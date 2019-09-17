using System.Windows;
using System.Windows.Controls;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Windows
{

    public static class ViewHelper
    {

        public static GroupHeader CreateGroupHeader(this FrameworkElement element, string header, string description) =>
            new GroupHeader
            {
                SeparatorStyle = element.TryFindResource("ParamGroupSeparator") as Style,
                HeaderTextStyle = element.TryFindResource("ParamGroupHeader") as Style,
                Header = header,
                Description = description
            };

        public static StackPanel AddGroupPanel(this Panel parent, string header, string description, int depth = 0)
        {
            var stackPanel = new StackPanel();
            if (depth > 0) stackPanel.Margin = new Thickness { Left = ViewConstants.Intend * depth };
            stackPanel.Children.Add(CreateGroupHeader(parent, header, description));
            parent.Children.Add(stackPanel);
            return stackPanel;
        }

        public static Grid AddRow(this Panel parent, string label, UIElement rightPart, uint rowHeight = 0) =>
            AddRow(parent, label == null ? null : new TextBlock { Text = label, Style = parent.TryFindResource("LabelText") as Style }, rightPart, rowHeight);

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

        public static TextBlock CreateParamNameTextBlock(this FrameworkElement element, IParameterDescriptor param) => new TextBlock
        {
            Style = (Style) element.TryFindResource("LabelText"),
            Text = param.Name + (param.Unit == null ? "" : $" ({param.Unit})"),
            ToolTip = $"Key: {param.Key}\nValue Type: {param.ValueType.GetFriendlyName()}{(param.Description == null ? "" : "\n" + param.Description)}",
        };

    }

}
