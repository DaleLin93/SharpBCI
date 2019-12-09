using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Extensions.IO.Devices;
using SharpBCI.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using JetBrains.Annotations;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Windows
{

    public class DeviceChangedEventArgs : EventArgs
    {

        public readonly DeviceType DeviceType;

        [CanBeNull] public readonly DeviceTemplate OldDevice, NewDevice;

        [NotNull] public readonly IReadonlyContext OldDeviceArgs;

        public IReadonlyContext NewDeviceArgs;

        public DeviceChangedEventArgs(DeviceType deviceType, DeviceTemplate oldDevice, DeviceTemplate newDevice, IReadonlyContext oldDeviceArgs)
        {
            DeviceType = deviceType;
            OldDevice = oldDevice;
            NewDevice = newDevice;
            OldDeviceArgs = oldDeviceArgs ?? EmptyContext.Instance;
        }

    }

    public class ConsumerChangedEventArgs : EventArgs
    {

        public readonly DeviceType DeviceType;

        [CanBeNull] public readonly ConsumerTemplate OldConsumer, NewConsumer;

        [NotNull] public readonly IReadonlyContext OldConsumerArgs;

        public IReadonlyContext NewConsumerArgs;

        public ConsumerChangedEventArgs(DeviceType deviceType, ConsumerTemplate oldConsumer, ConsumerTemplate newConsumer, IReadonlyContext oldConsumerArgs)
        {
            DeviceType = deviceType;
            OldConsumer = oldConsumer;
            NewConsumer = newConsumer;
            OldConsumerArgs = oldConsumerArgs ?? EmptyContext.Instance;
        }

    }

    public class DeviceSelectionPanel : StackPanel
    {

        public const int DeviceRowHeight = ViewConstants.DefaultRowHeight;

        internal class DeviceTypeViewModel
        {

            private const int ConsumerStateIndicatorSize = 10;

            private static readonly GridLength ZeroGridLength = new GridLength(0);

            private static readonly GridLength ConsumerStateGridLength = new GridLength(ViewConstants.MinorSpacing + ConsumerStateIndicatorSize);

            public readonly DeviceType DeviceType;

            [NotNull] public readonly Grid Container;

            [NotNull] public readonly ComboBox DeviceComboBox;

            [NotNull] public readonly Rectangle ConsumerStateRectangle;

            [NotNull] public readonly Button ConfigButton, PreviewButton;

            [CanBeNull] private TemplateWithArgs<ConsumerTemplate>[] _currentConsumers;

            internal DeviceTypeViewModel(DeviceType deviceType)
            {
                DeviceType = deviceType;

                Container = new Grid();
                Container.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.Star1GridLength});
                Container.ColumnDefinitions.Add(new ColumnDefinition {Width = ConsumerStateGridLength});
                Container.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.MinorSpacingGridLength});
                Container.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
                Container.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.MinorSpacingGridLength});
                Container.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});

                Container.Children.Add(DeviceComboBox = new ComboBox {Tag = this});
                Grid.SetColumn(DeviceComboBox, 0);

                Container.Children.Add(ConsumerStateRectangle = new Rectangle
                {
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Center,
                    Width = ConsumerStateIndicatorSize,
                    Height = ConsumerStateIndicatorSize, 
                    RadiusX = ConsumerStateIndicatorSize / 2.0, 
                    RadiusY = ConsumerStateIndicatorSize / 2.0, 
                    Tag = this
                });
                Grid.SetColumn(ConsumerStateRectangle, 1);

                Container.Children.Add(ConfigButton = CreateConfigButton(this));
                Grid.SetColumn(ConfigButton, 3);

                Container.Children.Add(PreviewButton = CreatePreviewButton(this));
                Grid.SetColumn(PreviewButton, 5);

                IsShowConsumerState = true;
                IsShowPreviewButton = true;
                SetConsumerState(null, false);
            }

            public bool IsShowConsumerState
            {
                set => Container.ColumnDefinitions[1].Width = value ? ConsumerStateGridLength : ZeroGridLength;
            }

            public bool IsShowPreviewButton
            {
                set
                {
                    if (value && DeviceType.DataVisualizer != null)
                    {
                        Container.ColumnDefinitions[4].Width = ViewConstants.MinorSpacingGridLength;
                        Container.ColumnDefinitions[5].Width = GridLength.Auto;
                    }
                    else
                    {
                        Container.ColumnDefinitions[4].Width = ZeroGridLength;
                        Container.ColumnDefinitions[5].Width = ZeroGridLength;
                    }
                }
            }

            [CanBeNull] public TemplateWithArgs<DeviceTemplate> CurrentDevice { get; set; }

            public TemplateWithArgs<ConsumerTemplate>[] CurrentConsumers
            {
                get => _currentConsumers ?? EmptyArray<TemplateWithArgs<ConsumerTemplate>>.Instance;
                set
                {
                    _currentConsumers = value;
                    UpdateConsumerState(_currentConsumers);
                }
            }

            public void SetConsumerState(string message, bool available)
            {
                ConsumerStateRectangle.Fill = available ? Brushes.SeaGreen : Brushes.DimGray;
                ConsumerStateRectangle.ToolTip = message ?? (available ? "Consumer attached" : "No consumer attached");
            }

            private void UpdateConsumerState(IReadOnlyCollection<TemplateWithArgs<ConsumerTemplate>> consumers)
            {
                var hasConsumers = consumers != null && consumers.Any();
                SetConsumerState(hasConsumers ? consumers.Select(c => c.Template.Identifier).Join("\n") : null, hasConsumers);
            }

        }

        public event EventHandler<DeviceChangedEventArgs> DeviceChanged;

        public event EventHandler<ConsumerChangedEventArgs> ConsumerChanged;

        private readonly ReferenceCounter _deviceUpdateLock = new ReferenceCounter();

        private readonly IDictionary<DeviceType, DeviceTypeViewModel> _deviceControlGroups = new Dictionary<DeviceType, DeviceTypeViewModel>(16);

        public DeviceSelectionPanel()
        {
            if (App.Instance == null) return;
            DeviceTypes = App.Instance.Registries.Registry<DeviceTypeAddOn>().Registered.Select(el => el.DeviceType).ToArray();
            Children.Add(ViewHelper.CreateGroupHeader(DisplayHeader ? "Devices" : null, "Device Configuration"));
            foreach (var deviceType in DeviceTypes)
            {
                var controlGroup = _deviceControlGroups[deviceType] = new DeviceTypeViewModel(deviceType);
                controlGroup.DeviceComboBox.SelectionChanged += DeviceComboBox_SelectionChanged;
                controlGroup.ConfigButton.Click += DeviceConfigBtn_Click;
                controlGroup.PreviewButton.Click += DevicePreviewBtn_Click;
                this.AddRow(deviceType.DisplayName, controlGroup.Container);
            }
            UpdateDevices();
        }

        internal static Button CreateConfigButton(object tag) => CreateIconButton("Config", ViewConstants.ConfigImageUri, 2, tag);

        internal static Button CreatePreviewButton(object tag) => CreateIconButton("Preview", ViewConstants.PreviewImageUri, 1, tag, false);

        internal static Button CreateIconButton(string tooltip, string imageUri, double imageMargin, object tag, bool enabled = true) => new Button
        {
            IsEnabled = enabled,
            FontSize = 10,
            Width = DeviceRowHeight,
            ToolTip = tooltip,
            Content = CreateImage(imageMargin, imageUri),
            Tag = tag
        };

        private static Image CreateImage(double margin, string imageUri) => new Image
        {
            Margin = new Thickness(margin),
            Source = new BitmapImage(new Uri(imageUri))
        };

        public DeviceConfig this[DeviceType deviceType]
        {
            get
            {
                if (!_deviceControlGroups.TryGetValue(deviceType, out var controlGroup) || controlGroup.CurrentDevice == null) return default;
                var device = controlGroup.CurrentDevice.Serialize();
                var consumers = controlGroup.CurrentConsumers.Select(c => c?.Serialize() ?? default);
                return new DeviceConfig(deviceType.Name, device, consumers.ToArray());
            }
            set
            {
                if (!_deviceControlGroups.TryGetValue(deviceType, out var controlGroup)) return;
                if (controlGroup.DeviceComboBox.FindAndSelectFirstByString(value.Device.Id, 0) && controlGroup.CurrentDevice != null)
                    controlGroup.CurrentDevice = new TemplateWithArgs<DeviceTemplate>(controlGroup.CurrentDevice.Template, value.Device.Args);
                var consumerRegistry = App.Instance.Registries.Registry<ConsumerTemplate>();
                var consumers = new LinkedList<TemplateWithArgs<ConsumerTemplate>>();
                foreach (var consumerEntity in value.Consumers?.Where(p => p.Id != null).ToArray() ?? EmptyArray<SerializedObject>.Instance)
                {
                    if(!consumerRegistry.LookUp(consumerEntity.Id, out var consumerTemplate)) continue;
                    consumers.AddLast(new TemplateWithArgs<ConsumerTemplate>(consumerTemplate, consumerEntity.Args));
                }
                controlGroup.CurrentConsumers = consumers.ToArray();
            }
        }

        public DeviceType[] DeviceTypes { get; }

        public bool DisplayHeader { get; set; } = true;

        public bool IsShowConsumerState
        {
            set
            {
                foreach (var group in _deviceControlGroups.Values)
                    group.IsShowConsumerState = value;
            }
        }

        public bool IsShowPreviewButton
        {
            set
            {
                foreach (var group in _deviceControlGroups.Values)
                    group.IsShowPreviewButton = value;
            }
        }

        public DeviceConfig[] DeviceConfigs
        {
            get
            {
                var dict = new Dictionary<string, DeviceConfig>();
                foreach (var deviceType in DeviceTypes)
                {
                    var deviceArgs = this[deviceType];
                    if (deviceArgs.DeviceType != null)
                        dict[deviceType.Name] = this[deviceType];
                }
                return dict.Values.ToArray();
            }
            set
            {
                var dict = new Dictionary<string, DeviceConfig>();
                foreach (var deviceArgs in value)
                    if (!dict.ContainsKey(deviceArgs.DeviceType))
                        dict[deviceArgs.DeviceType] = deviceArgs;
                foreach (var deviceType in DeviceTypes)
                    if (dict.TryGetValue(deviceType.Name, out var deviceArgs))
                        this[deviceType] = deviceArgs;
            }
        }

        public bool FindAndSelectDevice(DeviceType type, string itemStr, int? defaultIndex = null) =>
            _deviceControlGroups[type].DeviceComboBox.FindAndSelectFirstByString(itemStr, defaultIndex);

        [CanBeNull]
        public TemplateWithArgs<DeviceTemplate> GetDeviceWithArgs(DeviceType deviceType) =>
            _deviceControlGroups.TryGetValue(deviceType, out var controlGroup) ? controlGroup.CurrentDevice : null;

        [NotNull]
        public IEnumerable<TemplateWithArgs<ConsumerTemplate>> GetConsumersWithArgs(DeviceType deviceType) =>
            _deviceControlGroups.TryGetValue(deviceType, out var controlGroup) ? controlGroup.CurrentConsumers : EmptyArray<TemplateWithArgs<ConsumerTemplate>>.Instance;

        public void SetDeviceAndConsumers(DeviceType deviceType, TemplateWithArgs<DeviceTemplate> device, IEnumerable<TemplateWithArgs<ConsumerTemplate>> consumers)
        {
            if (!_deviceControlGroups.TryGetValue(deviceType, out var controlGroup)) return;
            if (device.Template.DeviceType != deviceType) throw new ArgumentException("device type not match");
            var streamingType = deviceType.StreamerFactory?.StreamingType;
            var consumerArray = consumers?.ToArray() ?? EmptyArray<TemplateWithArgs<ConsumerTemplate>>.Instance;
            if (consumerArray.Any(consumer => streamingType == null || !consumer.Template.AcceptType.IsAssignableFrom(streamingType)))
                throw new ArgumentException("device's streaming type not match consumer's accept type");
            controlGroup.CurrentDevice = device;
            controlGroup.CurrentConsumers = consumerArray;
        }

        public void UpdateDevices()
        {
            var devices = App.Instance.Registries.Registry<DeviceTemplate>().Registered;
            foreach (var deviceType in DeviceTypes)
            {
                var list = new LinkedList<object>();
                foreach (var device in devices.Where(d => d.DeviceType == deviceType))
                    list.AddLast(device);
                if (!deviceType.IsRequired) list.AddFirst(ViewHelper.CreateDefaultComboBoxItem());
                _deviceControlGroups[deviceType].DeviceComboBox.ItemsSource = list;
                _deviceControlGroups[deviceType].DeviceComboBox.SelectedIndex = 0;
            }
        }
        
        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var controlGroup = (DeviceTypeViewModel) ((ComboBox) sender).Tag;
            var newDevice = controlGroup.DeviceComboBox.SelectedItem as DeviceTemplate;
            controlGroup.PreviewButton.IsEnabled = newDevice?.Factory != null;
            if (_deviceUpdateLock.IsReferred) return;
            var cDevice = controlGroup.CurrentDevice;
            var eventArgs = new DeviceChangedEventArgs(controlGroup.DeviceType, cDevice?.Template, newDevice, cDevice?.Args);
            DeviceChanged?.Invoke(this, eventArgs);
            controlGroup.CurrentDevice = newDevice == null ? null : new TemplateWithArgs<DeviceTemplate>(newDevice, eventArgs.NewDeviceArgs);
        }

        private void DeviceConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            var controlGroup = (DeviceTypeViewModel) ((Button) sender).Tag;
            var deviceConfigWindow = new DeviceConfigWindow(controlGroup.DeviceType, controlGroup.CurrentDevice,
                controlGroup.CurrentConsumers.ToArray()) {Width = 500};
            deviceConfigWindow.DeviceChanged += (s0, e0) => DeviceChanged?.Invoke(this, e0);
            deviceConfigWindow.ConsumerChanged += (s0, e0) => ConsumerChanged?.Invoke(this, e0);
            if (!deviceConfigWindow.ShowDialog(out var device, out var consumers)) return;
            if (controlGroup.CurrentDevice?.Template != device?.Template)
                using (_deviceUpdateLock.Ref()) 
                    controlGroup.DeviceComboBox.FindAndSelectFirstByString(device?.Template.Identifier, 0);
            controlGroup.CurrentDevice = device;
            controlGroup.CurrentConsumers = consumers.ToArray();
        }

        private static void DevicePreviewBtn_Click(object sender, RoutedEventArgs e)
        {
            var controlGroup = (DeviceTypeViewModel) ((Button) sender).Tag;
            var deviceType = controlGroup.DeviceType;
            var selectedDevice = controlGroup.CurrentDevice?.Template;
            if (selectedDevice?.Factory == null) return;
            if (deviceType.DataVisualizer == null) return;
            var device = selectedDevice.NewInstance(controlGroup.CurrentDevice.Args);
            deviceType.DataVisualizer.Visualize(device);
        }

    }

}
