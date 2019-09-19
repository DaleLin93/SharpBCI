using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Extensions.Devices;
using SharpBCI.Extensions.Streamers;
using SharpBCI.Plugins;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Windows
{

    public class DeviceChangedEventArgs : EventArgs
    {

        public readonly DeviceType DeviceType;

        public readonly PluginDevice OldDevice, NewDevice;

        public readonly IReadonlyContext OldDeviceParams;

        public IReadonlyContext NewDeviceParams;

        public DeviceChangedEventArgs(DeviceType deviceType, PluginDevice oldDevice, PluginDevice newDevice, IReadonlyContext oldDeviceParams)
        {
            DeviceType = deviceType;
            OldDevice = oldDevice;
            NewDevice = newDevice;
            OldDeviceParams = oldDeviceParams;
        }

    }

    public class DeviceSelectionPanel : StackPanel
    {

        public const string NoneIdentifier = "<NONE>";

        public const int DeviceRowHeight = ViewConstants.DefaultRowHeight;

        public class DeviceControlGroup
        {

            private static readonly Uri ConfigImageUri = new Uri("pack://application:,,,/Resources/config.png", UriKind.Absolute);

            private static readonly Uri PreviewImageUri = new Uri("pack://application:,,,/Resources/preview.png", UriKind.Absolute);

            private static readonly GridLength Star1GridLength = new GridLength(1, GridUnitType.Star);

            private static readonly GridLength MinorSpacingGridLength = new GridLength(ViewConstants.MinorSpacing, GridUnitType.Pixel);

            public DeviceType DeviceType;

            public readonly ComboBox DeviceComboBox;

            public readonly Button ConfigButton, PreviewButton;

            internal DeviceControlGroup(DeviceType deviceType, bool designMode)
            {
                DeviceType = deviceType;
                Grid.SetColumn(DeviceComboBox = new ComboBox {Tag = this}, 0);
                Grid.SetColumn(ConfigButton = new Button
                {
                    IsEnabled = false,
                    FontSize = 10,
                    Width = DeviceRowHeight,
                    ToolTip = "Config",
                    Content = designMode ? null : CreateImage(2, ConfigImageUri),
                    Tag = this
                }, 2);
                Grid.SetColumn(PreviewButton = new Button
                {
                    IsEnabled = false,
                    FontSize = 10,
                    Width = DeviceRowHeight,
                    ToolTip = "Preview",
                    Content = designMode ? null : CreateImage(1, PreviewImageUri),
                    Tag = this
                }, 4);
            }

            private static Image CreateImage(double margin, Uri imageUri) => new Image
            {
                Margin = new Thickness(margin),
                Source = new BitmapImage(imageUri)
            };

            public Grid CreateContainer(bool addPreviewButton)
            {
                var container = new Grid();
                container.ColumnDefinitions.Add(new ColumnDefinition {Width = Star1GridLength});
                container.ColumnDefinitions.Add(new ColumnDefinition {Width = MinorSpacingGridLength});
                container.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
                container.Children.Add(DeviceComboBox);
                container.Children.Add(ConfigButton);
                if (addPreviewButton)
                {
                    container.ColumnDefinitions.Add(new ColumnDefinition {Width = MinorSpacingGridLength});
                    container.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
                    container.Children.Add(PreviewButton);
                }
                return container;
            }

        }

        public sealed class SelectedDevice
        {

            public PluginDevice Device { get; set; }

            public IReadonlyContext Params { get; set; } = EmptyContext.Instance;

        }

        public sealed class SelectedConsumer
        {

            public PluginStreamConsumer Consumer { get; set; }

            public IReadonlyContext Params { get; set; } = EmptyContext.Instance;

        }

        public event EventHandler<DeviceChangedEventArgs> DeviceChanged;

        public event EventHandler<ConsumerChangedEventArgs> ConsumerChanged;

        private readonly IDictionary<DeviceType, DeviceControlGroup> _deviceControlGroups;

        public DeviceSelectionPanel()
        {
            DeviceTypes = App.Instance.Registries.Registry<PluginDeviceType>().Registered.Select(el => el.DeviceType).ToArray();
            _deviceControlGroups = new Dictionary<DeviceType, DeviceControlGroup>(DeviceTypes.Length * 2);

            var designMode = DesignerProperties.GetIsInDesignMode(this);
            foreach (var deviceType in DeviceTypes)
            {
                var controlGroup = _deviceControlGroups[deviceType] = new DeviceControlGroup(deviceType, designMode);
                controlGroup.DeviceComboBox.SelectionChanged += DeviceComboBox_SelectionChanged;
                controlGroup.ConfigButton.Click += DeviceConfigBtn_Click;
                controlGroup.PreviewButton.Click += DevicePreviewBtn_Click;
                SelectedDevices[deviceType] = new SelectedDevice();
                SelectedConsumers[deviceType] = new SelectedConsumer();
            }
            InitializePanel();
            Loaded += (sender, args) => UpdateDevices();
        }

        public DeviceParams this[DeviceType deviceType]
        {
            get
            {
                var selectedDevice = SelectedDevices[deviceType];
                var device = PluginDevice.CreateParameterizedEntity(selectedDevice.Device, selectedDevice.Params);
                var selectedConsumer = SelectedConsumers[deviceType];
                var consumer = PluginStreamConsumer.CreateParameterizedEntity(selectedConsumer.Consumer, selectedConsumer.Params);
                return new DeviceParams { Device = device, Consumers = new[] { consumer } };
            }
            set
            {
                if (_deviceControlGroups[deviceType].DeviceComboBox.FindAndSelect(value.Device.Id ?? NoneIdentifier, null))
                {
                    var selectedDevice = SelectedDevices[deviceType];
                    selectedDevice.Params = selectedDevice.Device?.DeserializeParams(value.Device.Params) ?? (IReadonlyContext)EmptyContext.Instance;
                }
                var consumerEntity = value.Consumers.Length > 0 ? value.Consumers[0] : new ParameterizedEntity();
                App.Instance.Registries.Registry<PluginStreamConsumer>().LookUp(consumerEntity.Id ?? NoneIdentifier, out var registrableConsumer);
                SelectedConsumers[deviceType].Consumer = registrableConsumer;
                SelectedConsumers[deviceType].Params = registrableConsumer?.DeserializeParams(consumerEntity.Params) ?? (IReadonlyContext)EmptyContext.Instance;
            }
        }

        public DeviceType[] DeviceTypes { get; }

        public bool DisplayHeader { get; set; } = true;

        public bool IsPreviewButtonVisible { get; set; } = true;

        public IDictionary<DeviceType, SelectedDevice> SelectedDevices { get; set; } = new Dictionary<DeviceType, SelectedDevice>();

        public IDictionary<DeviceType, SelectedConsumer> SelectedConsumers { get; set; } = new Dictionary<DeviceType, SelectedConsumer>();

        public IDictionary<string, DeviceParams> DeviceConfig
        {
            get
            {
                var dict = new Dictionary<string, DeviceParams>();
                foreach (var deviceType in DeviceTypes) dict[deviceType.Name] = this[deviceType];
                return dict;
            }
            set
            {
                foreach (var deviceType in DeviceTypes)
                    if (value.TryGetValue(deviceType.Name, out var deviceParams))
                        this[deviceType] = deviceParams;
            }
        }

        public bool FindAndSelectDevice(DeviceType type, string itemStr, int? defaultIndex = null) => _deviceControlGroups[type].DeviceComboBox.FindAndSelect(itemStr, defaultIndex);

        public void UpdateDevices()
        {
            var registries = App.Instance?.Registries;
            if (registries == null) return;

            var devices = registries.Registry<PluginDevice>().Registered;
            foreach (var deviceType in DeviceTypes)
            {
                var list = new LinkedList<object>();
                foreach (var device in devices.Where(d => d.DeviceType == deviceType).OrderBy(d => d.Identifier))
                    list.AddLast(device);
                list.AddFirst(NoneIdentifier);
                _deviceControlGroups[deviceType].DeviceComboBox.ItemsSource = list;
            }
        }

        private void InitializePanel()
        {
            Children.Clear();
            Children.Add(this.CreateGroupHeader(DisplayHeader ? "Devices" : null, "Device Configuration"));
            foreach (var deviceType in DeviceTypes)
                this.AddRow(deviceType.DisplayName, _deviceControlGroups[deviceType].CreateContainer(IsPreviewButtonVisible), DeviceRowHeight);
        }

        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var controlGroup = (DeviceControlGroup) ((ComboBox) sender).Tag;
            var deviceType = controlGroup.DeviceType;
            var newDevice = controlGroup.DeviceComboBox.SelectedItem as PluginDevice;
            controlGroup.ConfigButton.IsEnabled = (newDevice?.Factory.Parameters?.Count ?? 0) > 0;
            controlGroup.PreviewButton.IsEnabled = newDevice?.Factory != null;
            var selectedDevice = SelectedDevices[deviceType];
            var eventArgs = new DeviceChangedEventArgs(controlGroup.DeviceType, selectedDevice.Device, newDevice, selectedDevice.Params);
            DeviceChanged?.Invoke(this, eventArgs);
            selectedDevice.Device = newDevice;
            selectedDevice.Params = eventArgs.NewDeviceParams ?? EmptyContext.Instance;
        }

        private void DeviceConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            var controlGroup = (DeviceControlGroup) ((Button) sender).Tag;
            var deviceType = controlGroup.DeviceType;
            var selectedItem = (PluginDevice) controlGroup.DeviceComboBox.SelectedItem;
            if (selectedItem?.Factory == null) return;
            var parameters = selectedItem.Factory.Parameters;
            if (CollectionUtils.IsNullOrEmpty(parameters)) return;
            var selectedDevice = SelectedDevices[deviceType];
            var selectedConsumer = SelectedConsumers[deviceType];
            var deviceConfigWindow = new DeviceConfigWindow(selectedItem, selectedDevice.Params,
                selectedConsumer.Consumer, selectedConsumer.Params) {Width = 500};
            deviceConfigWindow.ConsumerChanged += (s0, e0) => ConsumerChanged?.Invoke(this, e0);
            if (deviceConfigWindow.ShowDialog(out var deviceParams, out var consumer, out var consumerParams))
            {
                selectedDevice.Params = deviceParams;
                selectedConsumer.Consumer = consumer;
                selectedConsumer.Params = consumerParams;
            }
        }

        private void DevicePreviewBtn_Click(object sender, RoutedEventArgs e)
        {
            var controlGroup = (DeviceControlGroup) ((Button) sender).Tag;
            var deviceType = controlGroup.DeviceType;
            var selectedItem = (PluginDevice)controlGroup.DeviceComboBox.SelectedItem;
            if (selectedItem?.Factory == null) return;
            if (deviceType.BaseType == typeof(IEyeTracker))
            {
                var device = (IEyeTracker)selectedItem.NewInstance(SelectedDevices[deviceType].Params);
                new GazePointVisualizationWindow(new GazePointStreamer(device, Clock.SystemMillisClock), 50).Show();
            }
            else if (deviceType.BaseType == typeof(IBiosignalSampler))
            {
                var device = (IBiosignalSampler)selectedItem.NewInstance(SelectedDevices[deviceType].Params);
                new BiosignalVisualizationWindow(new BiosignalStreamer(device, Clock.SystemMillisClock), device.ChannelNum, (long)(device.Frequency * 5)).Show();
            }
            else if (deviceType.BaseType == typeof(IVideoSource))
            {
                var device = (IVideoSource)selectedItem.NewInstance(SelectedDevices[deviceType].Params);
                new VideoFramePresentationWindow(new VideoFrameStreamer(device, Clock.SystemMillisClock)).Show();
            }
        }

    }

}
