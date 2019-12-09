using System;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions.IO.Devices;
using SharpBCI.Extensions.Windows;
using SharpBCI.Plugins;

namespace SharpBCI.Windows
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for ParameterizedConfigWindow.xaml
    /// </summary>
    public partial class DeviceConfigWindow
    {

        public event EventHandler<DeviceChangedEventArgs> DeviceChanged;

        public event EventHandler<ConsumerChangedEventArgs> ConsumerChanged;

        public readonly DeviceConfigPanel DeviceConfigPanel;

        private TemplateWithArgs<DeviceTemplate> _device;

        private IReadOnlyList<TemplateWithArgs<ConsumerTemplate>> _consumers;

        public DeviceConfigWindow(DeviceType deviceType, [CanBeNull] TemplateWithArgs<DeviceTemplate> device, 
            [CanBeNull] IEnumerable<TemplateWithArgs<ConsumerTemplate>> consumers)
        {
            InitializeComponent();
            Title = $"{deviceType.DisplayName} Configuration";
            DockPanel.Children.Add(DeviceConfigPanel = new DeviceConfigPanel(deviceType, device, consumers));
            DeviceConfigPanel.DeviceChanged += DeviceChanged;
            DeviceConfigPanel.ConsumerChanged += ConsumerChanged;
        }

        public bool ShowDialog([CanBeNull] out TemplateWithArgs<DeviceTemplate> device, [NotNull] out IReadOnlyList<TemplateWithArgs<ConsumerTemplate>> consumers)
        {
            device = null;
            consumers = EmptyArray<TemplateWithArgs<ConsumerTemplate>>.Instance;
            var dialogResult = ShowDialog() ?? false;
            if (!dialogResult) return false;
            device = _device;
            consumers = _consumers;
            return true;
        }

        public void ResizeWindow(bool preferAnimation)
        {
            var contentHeight = DeviceConfigPanel.ContentHeight;
            var minWidth = DeviceConfigPanel.PreferredMinWidth;
            this.UpdateWindowSize(contentHeight + 50 + (ActualHeight - DeviceConfigPanel.ActualHeight), minWidth, preferAnimation);
        }

        private void Confirm()
        {
            try
            {
                DeviceConfigPanel.GetDeviceAndConsumers(out _device, out _consumers);
            }
            catch (DeviceConfigPanel.InvalidParametersException e)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append("The following parameters of device are invalid");
                foreach (var param in e.InvalidDeviceParameters) stringBuilder.Append("\n - ").Append(param.Name);
                var consumerIndex = 0;
                foreach (var consumerInvalidParams in e.InvalidConsumerParameters)
                {
                    stringBuilder.Append($"\nThe following parameters of consumer[{consumerIndex++}] are invalid");
                    foreach (var param in consumerInvalidParams) stringBuilder.Append("\n - ").Append(param.Name);
                }
                MessageBox.Show(stringBuilder.ToString());
                return;
            }
            DialogResult = true;
            Close();
        }

        private void Window_OnLoaded(object sender, EventArgs e) => ResizeWindow(false);

        private void Window_OnLayoutUpdated(object sender, EventArgs e)
        {
            if (!IsVisible || !DeviceConfigPanel.IsLayoutDirty || !IsLoaded) return;
            ResizeWindow(true);
            DeviceConfigPanel.IsLayoutDirty = false;
        }

        private void Window_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
            if (e.KeyStates == Keyboard.GetKeyStates(Key.Return) && Keyboard.Modifiers == ModifierKeys.Alt) Confirm();
        }

        private void OkBtn_OnClick(object sender, RoutedEventArgs e) => Confirm();

    }
}
