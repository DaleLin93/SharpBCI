using SharpBCI.Core.Experiment;
using System;
using System.Collections;
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

        private readonly Rectangle _separatorRectangle;

        private readonly TextBlock _headerTextBlock;

        public GroupHeader()
        {
            Children.Add(_separatorRectangle = new Rectangle {Margin = new Thickness {Left = 10, Right = 10, Top = 7}});
            Children.Add(_headerTextBlock = new TextBlock {Margin = new Thickness {Left = 15, Top = 2}, IsHitTestVisible = false, Visibility = Visibility.Hidden});
        }

        public Style SeparatorStyle
        {
            get => _separatorRectangle.Style;
            set => _separatorRectangle.Style = value;
        }

        public Style HeaderTextStyle
        {
            get => _headerTextBlock.Style;
            set => _headerTextBlock.Style = value;
        }

        public string Header
        {
            get => _headerTextBlock.Text;
            set
            {
                _headerTextBlock.Text = value;
                _headerTextBlock.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Hidden : Visibility.Visible;
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

    public sealed class GroupViewModel
    {

        [NotNull] public readonly IGroupDescriptor Group;

        [NotNull] public readonly StackPanel GroupPanel, ItemsPanel;

        public readonly int Depth;

        private bool _collapsed = false, _visible = true;

        public GroupViewModel(IGroupDescriptor group, StackPanel groupPanel, StackPanel itemsPanel, int depth)
        {
            Group = group;
            GroupPanel = groupPanel;
            ItemsPanel = itemsPanel;
            Depth = depth;
        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        [SuppressMessage("ReSharper", "SwitchStatementMissingSomeCases")]
        private static void ChangeVisibility(StackPanel stackPanel, Visibility visibility)
        {
            switch (visibility)
            {
                case Visibility.Visible:
                {
                    var contentHeight = stackPanel.Children.OfType<UIElement>()
                        .Sum(a => ((FrameworkElement)a).ActualHeight);
                    stackPanel.Height = 0;
                    var animation = ViewHelper.CreateDoubleAnimation(0, contentHeight, FillBehavior.Stop, 
                        () => stackPanel.DispatcherInvoke(sp => sp.Height = double.NaN));
                    stackPanel.BeginAnimation(FrameworkElement.HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
                    break;
                }
                case Visibility.Collapsed:
                {
                    var animation = ViewHelper.CreateDoubleAnimation(stackPanel.ActualHeight, 0, FillBehavior.Stop, 
                        () => stackPanel.DispatcherInvoke(sp => sp.Height = 0));
                    stackPanel.BeginAnimation(FrameworkElement.HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
                    break;
                }
                default:
                    throw new NotSupportedException(visibility.ToString());
            }
        }

        public bool IsVisible
        {
            get => _visible;
            set
            {
                if (_visible == value) return;
                _visible = value;
                ChangeVisibility(GroupPanel, _visible ? Visibility.Visible : Visibility.Collapsed);
            }
        }

        public bool IsCollapsed
        {
            get => _collapsed;
            set
            {
                if (_collapsed == value) return;
                _collapsed = value;
                ChangeVisibility(ItemsPanel, _collapsed ? Visibility.Collapsed : Visibility.Visible);
            }
        }

    }

    public sealed class ParamViewModel
    {

        [CanBeNull] public readonly GroupViewModel Group;

        [NotNull] public readonly KeyValueRow Container;

        [CanBeNull] public readonly TextBlock NameTextBlock;

        [NotNull] public readonly PresentedParameter PresentedParameter;

        private bool _visible = true;

        public ParamViewModel([CanBeNull] GroupViewModel group, [NotNull] KeyValueRow container, 
            [CanBeNull] TextBlock nameTextBlock, [NotNull] PresentedParameter presentedParameter)
        {
            Group = group;
            Container = container ?? throw new ArgumentNullException(nameof(container));
            NameTextBlock = nameTextBlock;
            PresentedParameter = presentedParameter ?? throw new ArgumentNullException(nameof(presentedParameter));
        }

        public IParameterDescriptor ParameterDescriptor => PresentedParameter.ParameterDescriptor;

        public bool IsVisible
        {
            get => _visible;
            set
            {
                if (_visible == value) return;
                _visible = value;
                UpdateVisibility();
            }
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

        private void UpdateVisibility()
        {
            switch (_visible)
            {
                case true:
                {
                    var contentHeight = (double)Container.Tag;
                    Container.Height = 0;
                    Container.Visibility = Visibility.Visible;
                        var animation = ViewHelper.CreateDoubleAnimation(0, contentHeight, FillBehavior.Stop,
                        () => Container.DispatcherInvoke(sp =>
                        {
                            sp.Height = double.NaN;

                        }));
                    Container.BeginAnimation(FrameworkElement.HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
                    break;
                }
                case false:
                {
                    Container.Tag = Container.ActualHeight;
                    var animation = ViewHelper.CreateDoubleAnimation(Container.ActualHeight, 0, FillBehavior.Stop,
                        () =>
                        {
                            Container.DispatcherInvoke(sp =>
                            {
                                sp.Height = double.NaN;
                                sp.Visibility = Visibility.Collapsed;
                            });
                        });
                    Container.BeginAnimation(FrameworkElement.HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
                    break;
                }
            }
        }

    }

    public sealed class SummaryViewModel
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

        public bool IsVisible
        {
            get => Container.Visibility == Visibility.Visible;
            set => Container.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }

    }

}
