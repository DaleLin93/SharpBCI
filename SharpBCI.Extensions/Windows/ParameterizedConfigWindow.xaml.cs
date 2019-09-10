using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.UI;

namespace SharpBCI.Extensions.Windows
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for ParameterizedConfigWindow.xaml
    /// </summary>
    public partial class ParameterizedConfigWindow
    {

        private bool _needAutoUpdateWindowSize;

        public ParameterizedConfigWindow([NotNull] string title, [NotNull] IEnumerable<IDescriptor> descriptors,
            IReadonlyContext context = null, IParameterPresentAdapter adapter = null)
        {
            InitializeComponent();
            ConfigurationPanel.Descriptors = descriptors.ToArray();
            ConfigurationPanel.Context = context ?? EmptyContext.Instance;
            ConfigurationPanel.Adapter = adapter;
            Title = title;
        }

        public bool ShowDialog(out IReadonlyContext @params)
        {
            @params = EmptyContext.Instance;
            var dialogResult = ShowDialog() ?? false;
            if (!dialogResult) return false;
            @params = ConfigurationPanel.Context;
            return true;
        }
        
        private void Confirm()
        {
            var invalidParams = ConfigurationPanel.GetInvalidParams().ToArray();
            if (invalidParams.Any())
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append("The following parameters are invalid");
                foreach (var param in invalidParams) stringBuilder.Append("\n - ").Append(param.Name);
                MessageBox.Show(stringBuilder.ToString());
                return;
            }
            DialogResult = true;
            Close();
        }

        private void Window_LayoutUpdated(object sender, EventArgs e)
        {
            if (!IsVisible || !_needAutoUpdateWindowSize) return;
            var point = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
            var screen = System.Windows.Forms.Screen.FromPoint(point.RoundToSdPoint());
            var scaleFactor = GraphicsUtils.Scale;
            var maxHeight = screen.WorkingArea.Height / scaleFactor;
            var contentHeight = StackPanel.Children.OfType<FrameworkElement>().Sum(el => el.ActualHeight);
            Height = MaxHeight = Math.Min(contentHeight + 20 + (ActualHeight - ScrollView.ActualHeight), maxHeight);
            var offset = screen.WorkingArea.Bottom / scaleFactor - (Top + ActualHeight);
            if (offset < 0) Top += offset;
            _needAutoUpdateWindowSize = false;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyStates == Keyboard.GetKeyStates(Key.Return) && Keyboard.Modifiers == ModifierKeys.Alt) Confirm();
        }

        private void ConfigurationPanel_LayoutChanged(object sender, LayoutChangedEventArgs e)
        {
            if (e.IsInitialization)
            {
                ScrollView.InvalidateScrollInfo();
                ScrollView.ScrollToTop();
            }
            _needAutoUpdateWindowSize = true;
        }

        private void ConfigurationPanel_ContextChanged(object sender, ContextChangedEventArgs e) { }

        private void OkBtn_Click(object sender, RoutedEventArgs e) => Confirm();

    }
}
