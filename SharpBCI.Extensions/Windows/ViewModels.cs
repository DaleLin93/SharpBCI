using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using JetBrains.Annotations;
using MarukoLib.UI;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.Extensions.Windows
{

    public class GroupHeader : Grid
    {

        public GroupHeader()
        {
            Children.Add(SeparatorRectangle = new Rectangle {Margin = new Thickness {Left = 10, Right = 10, Top = 7}});
            Children.Add(HeaderTextBlock = new TextBlock {Margin = new Thickness {Left = 15, Top = 2}, IsHitTestVisible = false, Visibility = Visibility.Hidden});

            Style = ViewHelper.GetResource("ParamGroupHeader") as Style;
            SeparatorRectangle.Style = ViewHelper.GetResource("ParamGroupHeaderLine") as Style;
            HeaderTextBlock.Style = ViewHelper.GetResource("ParamGroupHeaderText") as Style;
        }

        public Rectangle SeparatorRectangle { get; }

        public TextBlock HeaderTextBlock { get; }

        public string Header
        {
            get => HeaderTextBlock.Text;
            set
            {
                HeaderTextBlock.Text = value;
                HeaderTextBlock.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Hidden : Visibility.Visible;
            }
        }

        public string Description
        {
            get => ToolTip?.ToString();
            set => ToolTip = value;
        }

    }

    public class KeyValueRow : Grid
    {

        private const int AlertImageSize = 15;

        private static readonly ImageSource AlertImageSource = new BitmapImage(new Uri(ViewConstants.AlertImageUri));

        private readonly Rectangle _leftRect = new Rectangle
        {
            Fill = Brushes.Coral,
            Visibility = Visibility.Hidden
        };

        private readonly Rectangle _bgRect = new Rectangle
        {
            Fill = Brushes.LightPink,
            Stroke = Brushes.Coral,
            StrokeThickness = 1,
            Visibility = Visibility.Hidden
        };

        private readonly Image _alertImage = new Image
        {
            Source = AlertImageSource,
            HorizontalAlignment = HorizontalAlignment.Right,
            Visibility = Visibility.Hidden,
            Width = AlertImageSize, Height = AlertImageSize
        };

        private bool _err = false;

        public KeyValueRow(UIElement leftPart, UIElement rightPart)
        {
            Margin = ViewConstants.RowMargin;
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });
            RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto});
            RowDefinitions.Add(new RowDefinition { Height = new GridLength(2) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3) });
            ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.Star1GridLength, MaxWidth = 300});
            ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.MajorSpacingGridLength});
            ColumnDefinitions.Add(new ColumnDefinition {Width = new GridLength(2.5, GridUnitType.Star)});
            ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2) });

            Children.Add(_leftRect);
            SetRow(_leftRect, 0);
            SetRowSpan(_leftRect, 3);
            SetColumn(_leftRect, 0);
            Children.Add(_bgRect);
            SetRow(_bgRect, 0);
            SetRowSpan(_bgRect, 3);
            SetColumn(_bgRect, 1);
            SetColumnSpan(_bgRect, 8);
            Children.Add(_alertImage);
            SetRow(_alertImage, 1);
            SetColumn(_alertImage, 2);

            Children.Add(leftPart);
            SetRow(leftPart, 1);
            SetColumn(leftPart, 2);
            Children.Add(rightPart);
            SetRow(rightPart, 1);
            SetColumn(rightPart, 4);
        }

        public bool IsError
        {
            get => _err;
            set
            {
                if (_err == value) return;
                _err = value;
                _leftRect.Visibility = _bgRect.Visibility = _alertImage.Visibility = _err ? Visibility.Visible : Visibility.Hidden;
            }
        }

        public object ErrorMessage
        {
            get => _alertImage.ToolTip;
            set => _alertImage.ToolTip = value;
        }

    }

    public abstract class AnimatingViewModel
    {

        public event EventHandler AnimationCompleted;

        protected void UpdateVisibility(FrameworkElement element, bool visible, bool animate = true)
        {
            if (!animate)
            {
                element.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                element.Height = double.NaN;
                return;
            }
            switch (visible)
            {
                case true:
                {
                    element.Height = double.NaN;
                    element.Visibility = Visibility.Visible;
                    element.UpdateLayout();
                    element.Height = 0;
                    var contentHeight = element.DesiredSize.Height;
                    var animation = ViewHelper.CreateDoubleAnimation(0, contentHeight, FillBehavior.Stop,
                        () => element.DispatcherInvoke(sp =>
                        {
                            sp.Height = double.NaN;
                            AnimationCompleted?.Invoke(this, EventArgs.Empty);
                        }));
                    element.BeginAnimation(FrameworkElement.HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
                    break;
                }
                case false:
                {
                    element.Tag = element.ActualHeight;
                    var animation = ViewHelper.CreateDoubleAnimation(element.ActualHeight, 0, FillBehavior.Stop,
                        () => element.DispatcherInvoke(sp =>
                        {
                            sp.Height = double.NaN;
                            sp.Visibility = Visibility.Collapsed;
                            AnimationCompleted?.Invoke(this, EventArgs.Empty);
                        }));
                    element.BeginAnimation(FrameworkElement.HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
                    break;
                }
            }
        }

    }

    public sealed class GroupViewModel : AnimatingViewModel
    {

        [NotNull] public readonly IGroupDescriptor Group;

        [NotNull] public readonly StackPanel GroupPanel, ItemsPanel;

        public readonly int Depth;

        public GroupViewModel(IGroupDescriptor group, StackPanel groupPanel, StackPanel itemsPanel, int depth)
        {
            Group = group;
            GroupPanel = groupPanel;
            ItemsPanel = itemsPanel;
            Depth = depth;
        }

        public bool IsVisible { get; private set; } = true;

        public bool IsCollapsed { get; private set; } = false;

        public void SetVisible(bool value, bool animate = true)
        {
            if (IsVisible == value) return;
            IsVisible = value;
            UpdateVisibility(GroupPanel, IsVisible, animate);
        }

        public void SetCollapsed(bool value, bool animate = true)
        {
            if (IsCollapsed == value) return;
            IsCollapsed = value;
            UpdateVisibility(ItemsPanel, !IsCollapsed, animate);
        }

    }

    public sealed class ParamViewModel : AnimatingViewModel
    {

        [CanBeNull] public readonly GroupViewModel Group;

        [NotNull] public readonly KeyValueRow Container;

        [CanBeNull] public readonly TextBlock NameTextBlock;

        [NotNull] public readonly PresentedParameter PresentedParameter;

        public ParamViewModel([CanBeNull] GroupViewModel group, [NotNull] KeyValueRow container, 
            [CanBeNull] TextBlock nameTextBlock, [NotNull] PresentedParameter presentedParameter)
        {
            Group = group;
            Container = container ?? throw new ArgumentNullException(nameof(container));
            NameTextBlock = nameTextBlock;
            PresentedParameter = presentedParameter ?? throw new ArgumentNullException(nameof(presentedParameter));
        }

        public IParameterDescriptor ParameterDescriptor => PresentedParameter.ParameterDescriptor;

        public bool IsVisible { get; private set; } = true;

        public void SetVisible(bool value, bool animate = true)
        {
            if (IsVisible == value) return;
            IsVisible = value;
            UpdateVisibility(Container, IsVisible, animate);
        }

        public bool CheckValid()
        {
            try
            {
                PresentedParameter.GetValue();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }


    }

    public sealed class SummaryViewModel : AnimatingViewModel
    {

        [NotNull] public readonly ISummary Summary;

        [NotNull] public readonly Grid Container;

        [NotNull] public readonly TextBlock ValueTextBlock;

        public SummaryViewModel([NotNull] ISummary summary, [NotNull] Grid container, [NotNull] TextBlock valueTextBlock)
        {
            Summary = summary ?? throw new ArgumentNullException(nameof(summary));
            Container = container ?? throw new ArgumentNullException(nameof(container));
            ValueTextBlock = valueTextBlock ?? throw new ArgumentNullException(nameof(valueTextBlock));
        }

        public bool IsVisible { get; private set; } = true;

        public void SetVisible(bool value, bool animate = true)
        {
            if (IsVisible == value) return;
            IsVisible = value;
            UpdateVisibility(Container, IsVisible, animate);
        }

    }

}
