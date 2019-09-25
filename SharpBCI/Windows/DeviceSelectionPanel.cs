﻿using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Extensions.Devices;
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

        private class DeviceTypeViewModel
        {

            private const int ConsumerStateIndicatorSize = 10;

            private static readonly GridLength ZeroGridLength = new GridLength(0);

            private static readonly GridLength ConsumerStateGridLength = new GridLength(ViewConstants.MinorSpacing + ConsumerStateIndicatorSize);

            public readonly DeviceType DeviceType;

            [NotNull] public readonly Grid Container;

            [NotNull] public readonly ComboBox DeviceComboBox;

            [NotNull] public readonly Rectangle ConsumerStateRectangle;

            [NotNull] public readonly Button ConfigButton, PreviewButton;

            private Constructable<PluginStreamConsumer>[] _currentConsumers = null;

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

            public Constructable<PluginDevice> CurrentDevice { get; } = new Constructable<PluginDevice>();

            public Constructable<PluginStreamConsumer>[] CurrentConsumers
            {
                get => _currentConsumers ?? EmptyArray<Constructable<PluginStreamConsumer>>.Instance;
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

            private void UpdateConsumerState(IReadOnlyCollection<Constructable<PluginStreamConsumer>> consumers)
            {
                var hasConsumers = consumers != null && consumers.Any();
                SetConsumerState(hasConsumers ? consumers.Select(c => c.Target.Identifier).Join("\n") : null, hasConsumers);
            }

        }

        private sealed class Constructable<T>
        {

            public T Target { get; set; }

            public IReadonlyContext Params { get; set; } = EmptyContext.Instance;

            public static Constructable<T> Of(Tuple<T, IReadonlyContext> tuple) => new Constructable<T> {Target = tuple.Item1, Params = tuple.Item2};

            public static Tuple<T, IReadonlyContext> ToTuple(Constructable<T> c) => new Tuple<T, IReadonlyContext>(c.Target, c.Params);

        }

        public event EventHandler<DeviceChangedEventArgs> DeviceChanged;

        public event EventHandler<ConsumerChangedEventArgs> ConsumerChanged;

        private readonly IDictionary<DeviceType, DeviceTypeViewModel> _deviceControlGroups = new Dictionary<DeviceType, DeviceTypeViewModel>(16);

        public DeviceSelectionPanel()
        {
            DeviceTypes = App.Instance.Registries.Registry<PluginDeviceType>().Registered.Select(el => el.DeviceType).ToArray();
            foreach (var deviceType in DeviceTypes)
            {
                var controlGroup = _deviceControlGroups[deviceType] = new DeviceTypeViewModel(deviceType);
                controlGroup.DeviceComboBox.SelectionChanged += DeviceComboBox_SelectionChanged;
                controlGroup.ConfigButton.Click += DeviceConfigBtn_Click;
                controlGroup.PreviewButton.Click += DevicePreviewBtn_Click;
            }

            Children.Add(ViewHelper.CreateGroupHeader(DisplayHeader ? "Devices" : null, "Device Configuration"));
            foreach (var deviceType in DeviceTypes) this.AddRow(deviceType.DisplayName, _deviceControlGroups[deviceType].Container);

            Loaded += (sender, args) => UpdateDevices();
        }

        internal static Button CreateConfigButton(object tag) => CreateIconButton("Config", ViewConstants.ConfigImageUri, 2, tag);

        internal static Button CreatePreviewButton(object tag) => CreateIconButton("Preview", ViewConstants.PreviewImageUri, 1, tag);

        internal static Button CreateIconButton(string tooltip, string imageUri, double imageMargin, object tag) => new Button
        {
            IsEnabled = false,
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

        public DeviceParams this[DeviceType deviceType]
        {
            get
            {
                var controlGroup = _deviceControlGroups[deviceType];
                var currentDevice = controlGroup.CurrentDevice;
                var device = PluginDevice.CreateParameterizedEntity(currentDevice.Target, currentDevice.Params);
                var consumers = controlGroup.CurrentConsumers.Select(c => PluginStreamConsumer.CreateParameterizedEntity(c.Target, c.Params));
                return new DeviceParams {Device = device, Consumers = consumers.ToArray()};
            }
            set
            {
                var controlGroup = _deviceControlGroups[deviceType];
                if (controlGroup.DeviceComboBox.FindAndSelect(value.Device.Id ?? NoneIdentifier, null))
                {
                    var cDevice = controlGroup.CurrentDevice;
                    cDevice.Params = cDevice.Target?.DeserializeParams(value.Device.Params) ?? (IReadonlyContext)EmptyContext.Instance;
                }
                var consumerRegistry = App.Instance.Registries.Registry<PluginStreamConsumer>();
                var consumers = new LinkedList<Constructable<PluginStreamConsumer>>();
                foreach (var consumerEntity in value.Consumers?.Where(p => p.Id != null).ToArray() ?? EmptyArray<ParameterizedEntity>.Instance)
                {
                    if(!consumerRegistry.LookUp(consumerEntity.Id ?? NoneIdentifier, out var streamConsumer)) continue;
                    consumers.AddLast(new Constructable<PluginStreamConsumer>
                    {
                        Target = streamConsumer,
                        Params = streamConsumer?.DeserializeParams(consumerEntity.Params) ?? (IReadonlyContext) EmptyContext.Instance
                    });
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

        public bool FindAndSelectDevice(DeviceType type, string itemStr, int? defaultIndex = null) =>
            _deviceControlGroups[type].DeviceComboBox.FindAndSelect(itemStr, defaultIndex);

        public void UpdateDevices()
        {
            var devices = App.Instance.Registries.Registry<PluginDevice>().Registered;
            foreach (var deviceType in DeviceTypes)
            {
                var list = new LinkedList<object>();
                foreach (var device in devices.Where(d => d.DeviceType == deviceType).OrderBy(d => d.Identifier))
                    list.AddLast(device);
                if (!deviceType.IsRequired) list.AddFirst(NoneIdentifier);
                _deviceControlGroups[deviceType].DeviceComboBox.ItemsSource = list;
            }
        }
        
        private void DeviceComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var controlGroup = (DeviceTypeViewModel) ((ComboBox) sender).Tag;
            var newDevice = controlGroup.DeviceComboBox.SelectedItem as PluginDevice;
            controlGroup.ConfigButton.IsEnabled = newDevice?.Factory != null;
            controlGroup.PreviewButton.IsEnabled = newDevice?.Factory != null;
            var cDevice = controlGroup.CurrentDevice;
            var eventArgs = new DeviceChangedEventArgs(controlGroup.DeviceType, cDevice.Target, newDevice, cDevice.Params);
            DeviceChanged?.Invoke(this, eventArgs);
            cDevice.Target = newDevice;
            cDevice.Params = eventArgs.NewDeviceParams ?? EmptyContext.Instance;
        }

        private void DeviceConfigBtn_Click(object sender, RoutedEventArgs e)
        {
            var controlGroup = (DeviceTypeViewModel) ((Button) sender).Tag;
            var selectedItem = (PluginDevice) controlGroup.DeviceComboBox.SelectedItem;
            if (selectedItem?.Factory == null) return;
            var deviceConfigWindow = new DeviceConfigWindow(selectedItem, controlGroup.CurrentDevice.Params,
                controlGroup.CurrentConsumers.Select(Constructable<PluginStreamConsumer>.ToTuple).ToArray()) {Width = 500};
            deviceConfigWindow.ConsumerChanged += (s0, e0) => ConsumerChanged?.Invoke(this, e0);
            if (deviceConfigWindow.ShowDialog(out var deviceParams, out var consumers))
            {
                controlGroup.CurrentDevice.Params = deviceParams;
                controlGroup.CurrentConsumers = consumers.Select(Constructable<PluginStreamConsumer>.Of).ToArray();
            }
        }

        private static void DevicePreviewBtn_Click(object sender, RoutedEventArgs e)
        {
            var controlGroup = (DeviceTypeViewModel) ((Button) sender).Tag;
            var deviceType = controlGroup.DeviceType;
            var selectedItem = (PluginDevice)controlGroup.DeviceComboBox.SelectedItem;
            if (selectedItem?.Factory == null) return;
            if (deviceType.DataVisualizer == null) return;
            var device = selectedItem.NewInstance(controlGroup.CurrentDevice.Params);
            deviceType.DataVisualizer.Visualize(device);
        }

    }

}
