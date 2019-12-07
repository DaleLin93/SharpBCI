using System;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
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

        public static TextBlock CreateDefaultComboBoxItem(string text = ViewConstants.NotSelectedComboBoxItemText,
            TextAlignment alignment = TextAlignment.Left) => new TextBlock
        {
            Text = text,
            FontStyle = FontStyles.Italic,
            Foreground = Brushes.DimGray,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = alignment
            };

        public static void UpdateWindowSize(this Window window, double newHeight, double minWidth, bool animation = true)
        {
            var disableAnimation = animation && SystemVariables.DisableUiAnimation.Get(SystemVariables.Context);
            var point = window.PointToScreen(new Point(window.ActualWidth / 2, window.ActualHeight / 2));
            var screen = System.Windows.Forms.Screen.FromPoint(point.RoundToSdPoint());
            var scaleFactor = GraphicsUtils.Scale;
            var maxWidth = screen.WorkingArea.Width / scaleFactor;
            var maxHeight = screen.WorkingArea.Height / scaleFactor;
            minWidth = Math.Min(maxWidth, minWidth);
            newHeight = Math.Min(maxHeight, newHeight);
            var winWidth = window.Width;
            var winHeight = window.Height;
            var winRightEx = window.Left + minWidth + (window.ActualWidth - window.Width) - screen.WorkingArea.Right / scaleFactor;
            var winBottomEx = window.Top + newHeight + (window.ActualHeight - window.Height) - screen.WorkingArea.Bottom / scaleFactor;
            if (minWidth - winWidth > 1.0)
            {
                if (disableAnimation)
                    window.Width = minWidth;
                else
                    window.BeginAnimation(FrameworkElement.WidthProperty, CreateDoubleAnimation(winWidth, minWidth), HandoffBehavior.SnapshotAndReplace);
            }
            if (Math.Abs(newHeight - winHeight) > 1.0)
            {
                if (disableAnimation)
                    window.Height = newHeight;
                else
                    window.BeginAnimation(FrameworkElement.HeightProperty, CreateDoubleAnimation(winHeight, newHeight), HandoffBehavior.SnapshotAndReplace);
            }
            if (winRightEx > 0)
            {
                var newLeft = window.Left - winRightEx;
                if (disableAnimation)
                    window.Left = newLeft;
                else
                    window.BeginAnimation(Window.LeftProperty, CreateDoubleAnimation(window.Left, Math.Max(0, newLeft)), HandoffBehavior.SnapshotAndReplace);
            }
            // ReSharper disable once InvertIf
            if (winBottomEx > 0)
            {
                var newTop = window.Top - winBottomEx;
                if (disableAnimation)
                    window.Top = newTop;
                else
                    window.BeginAnimation(Window.TopProperty, CreateDoubleAnimation(window.Top, Math.Max(0, newTop)), HandoffBehavior.SnapshotAndReplace);
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

        public static DockPanel CreateGroupPanel([NotNull] GroupHeader groupHeader, int depth = 0)
        {
            var dockPanel = new DockPanel();
            if (depth > 0)
            {
                var groupIntendRectangle =  new Rectangle
                {
                    Width = 2,
                    Margin = new Thickness {Left = ViewConstants.Intend},
                    Fill = GetResource("LightSeparatorColorBrush") as Brush,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Stretch
                };
                dockPanel.Children.Add(groupIntendRectangle);
                DockPanel.SetDock(groupIntendRectangle, Dock.Left);
            }
            dockPanel.Children.Add(groupHeader);
            DockPanel.SetDock(groupHeader, Dock.Top);
            return dockPanel;
        }

        public static StackPanel AddGroupStackPanel(this Panel parent, string header, string description)
        {
            var stackPanel = new StackPanel();
            stackPanel.Children.Add(CreateGroupHeader(header, description));
            parent.Children.Add(stackPanel);
            return stackPanel;
        } 
            
        public static DockPanel AddGroupDockPanel(this Panel parent, [NotNull] GroupHeader groupHeader, int depth = 0)
        {
            var dockPanel = CreateGroupPanel(groupHeader, depth);
            parent.Children.Add(dockPanel);
            return dockPanel;
        }

        public static GroupViewModel CreateGroupViewModel(IGroupDescriptor group, int depth = 0, bool click2Collapse = false, Func<bool> collapseControl = null)
        {
            var groupHeader = CreateGroupHeader(group.Name, group.Description, click2Collapse);
            var dockPanel = CreateGroupPanel(groupHeader, depth);
            var itemsPanel = new StackPanel();
            dockPanel.Children.Add(itemsPanel);
            var viewModel = new GroupViewModel(group, dockPanel, itemsPanel, depth);
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
