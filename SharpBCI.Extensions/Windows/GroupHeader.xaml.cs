using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using JetBrains.Annotations;

namespace SharpBCI.Extensions.Windows
{

    /// <summary>
    /// Interaction logic for GroupHeader.xaml
    /// </summary>
    public partial class GroupHeader 
    {

        public static readonly DependencyProperty IsExpandableProperty = DependencyProperty.Register(nameof(IsExpandable), typeof(bool), typeof(GroupHeader), new UIPropertyMetadata(false));

        public static readonly DependencyProperty IsExpandedProperty = DependencyProperty.Register(nameof(IsExpanded), typeof(bool), typeof(GroupHeader), new UIPropertyMetadata(true));

        public GroupHeader() => InitializeComponent();

        public bool IsExpandable
        {
            get => (bool)GetValue(IsExpandableProperty);
            set => SetValue(IsExpandableProperty, value);
        }

        public bool IsExpanded
        {
            get => (bool)GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public string Header
        {
            get => HeaderTextBlock.Text;
            set
            {
                HeaderTextBlock.Text = value;
                HeaderTextBlock.Visibility = string.IsNullOrWhiteSpace(value) ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public string Description
        {
            get => ToolTip?.ToString();
            set => ToolTip = value;
        }

    }

}
