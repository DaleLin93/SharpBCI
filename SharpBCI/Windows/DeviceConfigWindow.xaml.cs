using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices;
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

        private class EntityConfigViewModel<T>
        {

            internal readonly StackPanel Container;

            internal readonly ComboBox ComboBox;

            internal readonly ParameterPanel ParamPanel;
            
            internal T Current;

            internal EntityConfigViewModel(string type, string groupDesc, IEnumerable selections) : this(type, groupDesc, new StackPanel(), selections) { }

            internal EntityConfigViewModel(string type, string groupDesc, StackPanel container, IEnumerable selections)
            {
                Container = container;
                var consumerGroupPanel = Container.AddGroupPanel(type, groupDesc);
                ComboBox = new ComboBox {ItemsSource = selections, Tag = this};
                consumerGroupPanel.AddRow(type, ComboBox);
                ParamPanel = new ParameterPanel {AllowCollapse = false, Tag = this};
                Container.Children.Add(ParamPanel);
            }

        }

        private class DeviceConfigViewModel : EntityConfigViewModel<PluginDevice>
        {
            public DeviceConfigViewModel(StackPanel container, IEnumerable devices) : base("Device", "Device Selection", container, devices) { }
        }

        private class ConsumerConfigViewModel : EntityConfigViewModel<PluginStreamConsumer>
        {
            public ConsumerConfigViewModel(IEnumerable consumers) : base("Consumer", "Consumer Selection", consumers) { }
        }

        public event EventHandler<DeviceChangedEventArgs> DeviceChanged;

        public event EventHandler<ConsumerChangedEventArgs> ConsumerChanged;

        private readonly ReferenceCounter _deviceParamsUpdateLock = new ReferenceCounter();

        private readonly ReferenceCounter _consumerParamsUpdateLock = new ReferenceCounter();

        private readonly DeviceType _deviceType;

        private readonly DeviceConfigViewModel _deviceViewModel;

        private readonly LinkedList<ConsumerConfigViewModel> _consumerViewModels = new LinkedList<ConsumerConfigViewModel>();

        private bool _loadCompleted;

        private bool _needResizeWindow;

        public DeviceConfigWindow(DeviceType deviceType, [CanBeNull] PluginDevice device, [CanBeNull] IReadonlyContext deviceParams,
            [CanBeNull] IReadOnlyCollection<Tuple<PluginStreamConsumer, IReadonlyContext>> consumers)
        {
            InitializeComponent();
            Title = $"{deviceType.DisplayName} Configuration";

            _deviceType = deviceType;

            _deviceViewModel = new DeviceConfigViewModel(DeviceConfigContainer, GetDeviceList(_deviceType));
            _deviceViewModel.ComboBox.SelectionChanged += DeviceComboBox_OnSelectionChanged;
            _deviceViewModel.ComboBox.SelectedIndex = 0;
            _deviceViewModel.ParamPanel.ContextChanged += ConfigurationPanel_OnContextChanged;
            _deviceViewModel.ParamPanel.LayoutChanged += ConfigurationPanel_OnLayoutChanged;
            _deviceViewModel.Current = device;

            Loaded += (sender, e) =>
            {
                using (_consumerParamsUpdateLock.Ref())
                    _deviceViewModel.ComboBox.FindAndSelectFirstByString(device?.Identifier, 0);
                _deviceViewModel.ParamPanel.Context = deviceParams ?? EmptyContext.Instance;

                if (consumers != null)
                    foreach (var consumer in consumers)
                    {
                        if (string.IsNullOrWhiteSpace(consumer.Item1?.Identifier)) continue;
                        var viewModel = AddConsumerConfig();
                        using (_consumerParamsUpdateLock.Ref())
                            viewModel.ComboBox.FindAndSelectFirstByString(consumer.Item1.Identifier, 0);
                        viewModel.ParamPanel.Context = consumer.Item2 ?? EmptyContext.Instance;
                    }

                if (!_consumerViewModels.Any()) AddConsumerConfig();
                _loadCompleted = true;
            };
        }

        private ConsumerConfigViewModel AddConsumerConfig()
        {
            var viewModel = new ConsumerConfigViewModel(GetConsumerList(_deviceType));
            viewModel.ComboBox.SelectionChanged += ConsumerComboBox_OnSelectionChanged;
            viewModel.ComboBox.SelectedIndex = 0;
            viewModel.ParamPanel.ContextChanged += ConfigurationPanel_OnContextChanged;
            viewModel.ParamPanel.LayoutChanged += ConfigurationPanel_OnLayoutChanged;
            _consumerViewModels.AddLast(viewModel);
            ConsumersStackPanel.Children.Add(viewModel.Container);
            return viewModel;
        }

        private static IEnumerable<IDescriptor> AsGroup(IEnumerable<IDescriptor> @params)
        {
            if (@params == null) return EmptyArray<IDescriptor>.Instance;
            var array = @params.ToArray();
            return array.Length == 0 ? array : new IDescriptor[] {new ParameterGroup(array) };
        }

        private static IList GetDeviceList(DeviceType deviceType)
        {
            var list = new List<object>();
            if (!deviceType.IsRequired) list.Add(ViewHelper.CreateDefaultComboBoxItem());
            list.AddRange(App.Instance.Registries.Registry<PluginDevice>().Registered.Where(pd => pd.DeviceType == deviceType));
            return list;
        }

        private static IList GetConsumerList(DeviceType deviceType)
        {
            var streamerValueType = deviceType.StreamerFactory?.StreamingType;
            var list = new List<object> {ViewHelper.CreateDefaultComboBoxItem()};
            if (streamerValueType == null) return list;
            list.AddRange(App.Instance.Registries.Registry<PluginStreamConsumer>().Registered
                .Where(pc => pc.Factory.GetAcceptType(pc.ConsumerClass).IsAssignableFrom(streamerValueType)));
            return list;
        }

        public bool ShowDialog([NotNull] out Tuple<PluginDevice, IReadonlyContext> device, [NotNull] out IReadOnlyCollection<Tuple<PluginStreamConsumer, IReadonlyContext>> consumers)
        {
            device = new Tuple<PluginDevice, IReadonlyContext>(null, EmptyContext.Instance);
            consumers = EmptyArray<Tuple<PluginStreamConsumer, IReadonlyContext>>.Instance;
            var dialogResult = ShowDialog() ?? false;
            if (!dialogResult) return false;
            device = new Tuple<PluginDevice, IReadonlyContext>(_deviceViewModel.Current, _deviceViewModel.ParamPanel.Context);
            var consumerList = new List<Tuple<PluginStreamConsumer, IReadonlyContext>>(_consumerViewModels.Count);
            consumerList.AddRange(from viewModel in _consumerViewModels where viewModel.Current != null 
                select new Tuple<PluginStreamConsumer, IReadonlyContext>(viewModel.Current, viewModel.ParamPanel.Context));
            consumers = consumerList;
            return true;
        }

        private void InitializeDeviceConfigurationPanel(PluginDevice device)
        {
            _deviceViewModel.Current = device;
            _deviceViewModel.ParamPanel.SetDescriptors(device?.Factory as IParameterPresentAdapter, AsGroup(device?.Factory.GetParameters(device.DeviceClass)));

            ScrollView.InvalidateScrollInfo();
            _needResizeWindow = IsLoaded;
        }

        private void InitializeConsumerConfigurationPanel(ConsumerConfigViewModel consumerConfigViewModel, PluginStreamConsumer consumer)
        {
            consumerConfigViewModel.Current = consumer;
            consumerConfigViewModel.ParamPanel.SetDescriptors(consumer?.Factory as IParameterPresentAdapter, AsGroup(consumer?.Factory.GetParameters(consumer.ConsumerClass)));

            ScrollView.InvalidateScrollInfo();
            _needResizeWindow = IsLoaded;
        }

        private void Confirm()
        {
            var deviceInvalidParams = _deviceViewModel.ParamPanel.GetInvalidParams().ToArray();
            var consumerInvalidParamsArray = _consumerViewModels.Select(vm => vm.ParamPanel.GetInvalidParams().ToArray()).ToArray();
            if (deviceInvalidParams.Any() || (consumerInvalidParamsArray.Sum(p => (int?)p.Length) ?? 0) > 0)
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append("The following parameters of device are invalid");
                foreach (var param in deviceInvalidParams) stringBuilder.Append("\n - ").Append(param.Name);
                var consumerIndex = 0;
                foreach (var consumerInvalidParams in consumerInvalidParamsArray)
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

        private void Window_LayoutUpdated(object sender, EventArgs e)
        {
            if (!IsVisible || !_needResizeWindow || !IsLoaded) return;
            var contentHeight = StackPanel.Children.OfType<FrameworkElement>().Sum(el => el.ActualHeight);
            this.UpdateWindowHeight(contentHeight + 50 + (ActualHeight - ScrollView.ActualHeight), _loadCompleted);
            _needResizeWindow = false;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape) Close();
            if (e.KeyStates == Keyboard.GetKeyStates(Key.Return) && Keyboard.Modifiers == ModifierKeys.Alt) Confirm();
        }

        private void ConfigurationPanel_OnLayoutChanged(object sender, LayoutChangedEventArgs e)
        {
            if (e.IsInitialization) ScrollView.InvalidateScrollInfo();
            _needResizeWindow = true;
        }

        private void ConfigurationPanel_OnContextChanged(object sender, ContextChangedEventArgs e) { }

        private void DeviceComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            var viewModel = (DeviceConfigViewModel)comboBox.Tag;
            var device = comboBox.SelectedItem as PluginDevice;
            InitializeDeviceConfigurationPanel(device);

            if (_deviceParamsUpdateLock.IsReferred) return;
            var eventArgs = new DeviceChangedEventArgs(_deviceType, viewModel.Current, device, viewModel.ParamPanel.Context);
            DeviceChanged?.Invoke(this, eventArgs);
            viewModel.ParamPanel.Context = eventArgs.NewDeviceParams ?? EmptyContext.Instance;
        }

        private void ConsumerComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (ComboBox) sender;
            var viewModel = (ConsumerConfigViewModel) comboBox.Tag;
            var streamConsumer = comboBox.SelectedItem as PluginStreamConsumer;
            InitializeConsumerConfigurationPanel(viewModel, streamConsumer);

            if (_consumerParamsUpdateLock.IsReferred) return;
            var eventArgs = new ConsumerChangedEventArgs(_deviceType, viewModel.Current, streamConsumer, viewModel.ParamPanel.Context);
            ConsumerChanged?.Invoke(this, eventArgs);
            viewModel.ParamPanel.Context = eventArgs.NewConsumerParams ?? EmptyContext.Instance;
        }

        private void AddConsumerButton_OnClick(object sender, RoutedEventArgs e) => AddConsumerConfig();

        private void OkBtn_Click(object sender, RoutedEventArgs e) => Confirm();

    }
}
