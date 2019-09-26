using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.UI;

namespace SharpBCI.Extensions.Windows
{

    public static class ViewHelper
    {

        public static readonly ResourceDictionary Resources = new ResourceDictionary {Source = new Uri(ViewConstants.SharedResourceDictionaryUri)};

        public static object GetResource(string name)
        {
            var res = Resources[name];
            if (res == DependencyProperty.UnsetValue) throw new ProgrammingException($"Resource not found by name: '{name}'");
            return res;
        }

        public static void UpdateWindowHeight(this Window window, double newHeight)
        {
            var point = window.PointToScreen(new Point(window.ActualWidth / 2, window.ActualHeight / 2));
            var screen = System.Windows.Forms.Screen.FromPoint(point.RoundToSdPoint());
            var scaleFactor = GraphicsUtils.Scale;
            var maxHeight = screen.WorkingArea.Height / scaleFactor;
            newHeight = Math.Min(maxHeight, newHeight);
            var height = window.Height;
            var disableAnimation = SystemVariables.DisableUiAnimation.Get(SystemVariables.Context);
            if (Math.Abs(newHeight - height) > 1.0)
            {
                if (disableAnimation)
                    window.Height = newHeight;
                else
                    window.BeginAnimation(FrameworkElement.HeightProperty, CreateDoubleAnimation(height, newHeight),
                        HandoffBehavior.SnapshotAndReplace);
            }
            var offset = screen.WorkingArea.Bottom / scaleFactor - (window.Top + newHeight + (window.ActualHeight - window.Height));
            if (offset < 0)
            {
                var newTop = window.Top + offset;
                if (disableAnimation)
                    window.Top = newTop;
                else
                    window.BeginAnimation(Window.TopProperty, CreateDoubleAnimation(window.Top, Math.Max(0, newTop)),
                        HandoffBehavior.SnapshotAndReplace);
            }
        }

        public static DoubleAnimation CreateDoubleAnimation(double from, double to, FillBehavior? fillBehavior = null,
            Action completeAction = null,
            Duration? duration = null, IEasingFunction easingFunction = null)
        {
            var animation = new DoubleAnimation(from, to, duration ?? ViewConstants.DefaultAnimationDuration)
            {
                FillBehavior = fillBehavior ?? FillBehavior.HoldEnd,
                EasingFunction = easingFunction ?? ViewConstants.DefaultEasingFunction
            };
            if (completeAction != null) animation.Completed += (sender, e) => completeAction();
            return animation;
        }

        public static GroupHeader CreateGroupHeader(string header, string description, bool click2Collapse = false)
        {
            var tooltipBuilder = new StringBuilder(64).Append(header);
            if (!string.IsNullOrEmpty(description)) tooltipBuilder.AppendIfEmpty('\n').Append(description);
            if (click2Collapse) tooltipBuilder.AppendIfEmpty('\n').Append("(Click to Collapse/Expand)");
            return new GroupHeader
            {
                Header = header,
                Description = description,
                ToolTip = tooltipBuilder.ToString()
            };
        }

        public static TextBlock CreateParamNameTextBlock(IParameterDescriptor param, bool dbClick2Reset = false)
        {
            var tooltipBuilder = new StringBuilder(64);
            tooltipBuilder.Append("Key: ").Append(param.Key);
            tooltipBuilder.Append("\nValue Type: ").Append(param.ValueType.GetFriendlyName());
            if (!string.IsNullOrEmpty(param.Description)) tooltipBuilder.Append('\n').Append(param.Description);
            if (dbClick2Reset) tooltipBuilder.Append("\n(Double-Click to Reset)");
            return new TextBlock
            {
                Style = (Style) GetResource("LabelText"),
                Text = param.Name + (param.Unit == null ? "" : $" ({param.Unit})"),
                ToolTip = tooltipBuilder.ToString()
            };
        }

        public static StackPanel AddGroupPanel(this Panel parent, string header, string description, int depth = 0) => 
            AddGroupPanel(parent, CreateGroupHeader(header, description), depth);

        public static StackPanel AddGroupPanel(this Panel parent, [NotNull] GroupHeader groupHeader, int depth = 0)
        {
            var stackPanel = new StackPanel();
            if (depth > 0) stackPanel.Margin = new Thickness { Left = ViewConstants.Intend * depth };
            stackPanel.Children.Add(groupHeader);
            parent.Children.Add(stackPanel);
            return stackPanel;
        }

        public static GroupViewModel CreateGroupViewModel(IGroupDescriptor group, int depth = 0, bool click2Collapse = false, Func<bool> collapseControl = null)
        {
            var stackPanel = new StackPanel();
            if (depth > 0) stackPanel.Margin = new Thickness { Left = ViewConstants.Intend * depth };
            var groupHeader = CreateGroupHeader(group.Name, group.Description, click2Collapse);
            stackPanel.Children.Add(groupHeader);
            var itemsPanel = new StackPanel();
            stackPanel.Children.Add(itemsPanel);
            var viewModel = new GroupViewModel(group, stackPanel, itemsPanel, depth);
            if (click2Collapse) groupHeader.MouseLeftButtonUp += (sender, e) =>
            {
                var collapse = !viewModel.IsCollapsed;
                if (collapse && collapseControl != null && !collapseControl()) return;
                viewModel.SetCollapsed(collapse);
            };
            return viewModel;
        }

        public static KeyValueRow AddRow(this Panel parent, string label, UIElement rightPart, uint rowHeight = 0) =>
            AddRow(parent, label == null ? null : new TextBlock { Text = label, Style = GetResource("LabelText") as Style }, rightPart, rowHeight);

        public static KeyValueRow AddRow(this Panel parent, UIElement leftPart, UIElement rightPart, uint rowHeight = 0)
        {
            var row = new KeyValueRow(leftPart, rightPart);
            if (rowHeight > 0) row.Height = rowHeight;
            parent.Children.Add(row);
            return row; 
        }

    }

}
