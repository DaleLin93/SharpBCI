using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Extensions;
using SharpBCI.Extensions.IO.Devices;
using SharpBCI.Extensions.Windows;
using SharpBCI.Plugins;

namespace SharpBCI.Windows
{

    /// <inheritdoc cref="DockPanel" />
    /// <summary>
    /// Interaction logic for ParameterizedConfigWindow.xaml
    /// </summary>
    public partial class DeviceConfigPanel
    {

        public const int MaxConsumerCount = 16;

        public class InvalidParametersException : Exception
        {

            [NotNull] public readonly IParameterDescriptor[] InvalidDeviceParameters;

            [NotNull] public readonly IParameterDescriptor[][] InvalidConsumerParameters;

            public InvalidParametersException(
                [NotNull] IParameterDescriptor[] invalidDeviceParameters,
                [NotNull] IParameterDescriptor[][] invalidConsumerParameters)
            {
                InvalidDeviceParameters = invalidDeviceParameters;
                InvalidConsumerParameters = invalidConsumerParameters;
            }

        }

        private class ConfigViewModel<T>
        {

            internal readonly StackPanel Container;

            internal readonly ComboBox ComboBox;

            internal readonly ParameterPanel ParamPanel;

            internal T Current;

            internal ConfigViewModel(string type, string groupDesc, IEnumerable selections) : this(type, groupDesc, new StackPanel(), selections) { }

            internal ConfigViewModel(string type, string groupDesc, StackPanel container, IEnumerable selections)
            {
                Container = container;
                var consumerGroupPanel = Container.AddGroupStackPanel(type, groupDesc);
                ComboBox = new ComboBox {ItemsSource = selections, Tag = this};
                consumerGroupPanel.AddRow(type, ComboBox);
                ParamPanel = new ParameterPanel {AllowCollapse = false, Tag = this};
                Container.Children.Add(ParamPanel);
            }

        }

        private class DeviceConfigViewModel : ConfigViewModel<DeviceTemplate>
        {
            public DeviceConfigViewModel(StackPanel container, IEnumerable devices) : base("Device", "Device Selection", container, devices) { }
        }

        private class ConsumerConfigViewModel : ConfigViewModel<ConsumerTemplate>
        {
            public ConsumerConfigViewModel(IEnumerable consumers) : base("Consumer", "Consumer Selection", consumers) { }
        }

        public event EventHandler<DeviceChangedEventArgs> DeviceChanged;

        public event EventHandler<ConsumerChangedEventArgs> ConsumerChanged;

        private readonly ReferenceCounter _deviceUpdateLock = new ReferenceCounter();

        private readonly ReferenceCounter _consumerUpdateLock = new ReferenceCounter();

        private readonly DeviceType _deviceType;

        private readonly DeviceConfigViewModel _deviceViewModel;

        private readonly LinkedList<ConsumerConfigViewModel> _consumerViewModels = new LinkedList<ConsumerConfigViewModel>();

        public DeviceConfigPanel(DeviceType deviceType, [CanBeNull] TemplateWithArgs<DeviceTemplate> device,
            [CanBeNull] IEnumerable<TemplateWithArgs<ConsumerTemplate>> consumers)
        {
            InitializeComponent();

            _deviceType = deviceType;

            _deviceViewModel = new DeviceConfigViewModel(DeviceConfigContainer, GetDeviceList(_deviceType));
            _deviceViewModel.ComboBox.SelectionChanged += DeviceComboBox_OnSelectionChanged;
            _deviceViewModel.ComboBox.FindAndSelectFirstByString(device?.Template.Identifier, 0);
            _deviceViewModel.ParamPanel.Context = device?.Args ?? EmptyContext.Instance;
            _deviceViewModel.ParamPanel.LayoutChanged += ConfigurationPanel_OnLayoutChanged;
            _deviceViewModel.Current = device?.Template;

            if (consumers != null)
                foreach (var consumer in consumers)
                {
                    if (string.IsNullOrWhiteSpace(consumer?.Template.Identifier)) continue;
                    AppendConsumerConfig(consumer);
                }

            if (!_consumerViewModels.Any()) AppendConsumerConfig();
        }

        public bool IsLayoutDirty { get; set; }

        public double ContentHeight => StackPanel.Children.OfType<FrameworkElement>().Sum(el => el.ActualHeight);

        public double PreferredMinWidth => _consumerViewModels.Select(vm => vm.Current?.Factory as IPresentAdapter)
            .Concat(new[] { _deviceViewModel.Current?.Factory as IPresentAdapter }).Filter(Predicates.NotNull)
            .GetPreferredMinWidth(0);

        private void AppendConsumerConfig(TemplateWithArgs<ConsumerTemplate> consumer = null)
        {
            var viewModel = new ConsumerConfigViewModel(GetConsumerList(_deviceType));
            viewModel.ComboBox.SelectionChanged += ConsumerComboBox_OnSelectionChanged;
            viewModel.ComboBox.FindAndSelectFirstByString(consumer?.Template.Identifier, 0);
            viewModel.ParamPanel.Context = consumer?.Args ?? EmptyContext.Instance;
            viewModel.ParamPanel.LayoutChanged += ConfigurationPanel_OnLayoutChanged;
            _consumerViewModels.AddLast(viewModel);
            ConsumersStackPanel.Children.Add(viewModel.Container);
            if (_consumerViewModels.Count >= MaxConsumerCount)
            {
                AppendConsumerButton.IsEnabled = false;
                AppendConsumerButton.Content = "MAX";
            }
        }

        private static IEnumerable<IDescriptor> AsGroup(IEnumerable<IDescriptor> @params)
        {
            if (@params == null) return EmptyArray<IDescriptor>.Instance;
            var array = @params.ToArray();
            return array.Length == 0 ? array : new IDescriptor[] {new ParameterGroup(array)};
        }

        private static IList GetDeviceList(DeviceType deviceType)
        {
            var list = new List<object>();
            if (!deviceType.IsRequired) list.Add(ViewHelper.CreateDefaultComboBoxItem());
            list.AddRange(App.Instance.Registries.Registry<DeviceTemplate>().Registered.Where(pd => pd.DeviceType == deviceType));
            return list;
        }

        private static IList GetConsumerList(DeviceType deviceType)
        {
            var streamerValueType = deviceType.StreamerFactory?.StreamingType;
            var list = new List<object> {ViewHelper.CreateDefaultComboBoxItem()};
            if (streamerValueType == null) return list;
            list.AddRange(App.Instance.Registries.Registry<ConsumerTemplate>().Registered
                .Where(pc => pc.Factory.GetAcceptType(pc.Clz).IsAssignableFrom(streamerValueType)));
            return list;
        }

        /// <summary>
        /// Get device config and consumer configs.
        /// </summary>
        /// <param name="device"></param>
        /// <param name="consumers"></param>
        /// <exception cref="InvalidParametersException"></exception>
        public void GetDeviceAndConsumers(out TemplateWithArgs<DeviceTemplate> device, out IReadOnlyList<TemplateWithArgs<ConsumerTemplate>> consumers)
        {
            var invalidDeviceParams = _deviceViewModel.ParamPanel.GetInvalidParams().ToArray();
            var invalidConsumerParamsArray = _consumerViewModels.Select(vm => vm.ParamPanel.GetInvalidParams().ToArray()).ToArray();
            if (invalidDeviceParams.Any() || (invalidConsumerParamsArray.Sum(p => (int?)p.Length) ?? 0) > 0)
                throw new InvalidParametersException(invalidDeviceParams, invalidConsumerParamsArray);
            device = new TemplateWithArgs<DeviceTemplate>(_deviceViewModel.Current, _deviceViewModel.ParamPanel.Context);
            var consumerList = new List<TemplateWithArgs<ConsumerTemplate>>(_consumerViewModels.Count);
            consumerList.AddRange(from viewModel in _consumerViewModels
                where viewModel.Current != null
                select new TemplateWithArgs<ConsumerTemplate>(viewModel.Current, viewModel.ParamPanel.Context));
            consumers = consumerList;
        }

        private void InitializeDeviceConfigurationPanel(DeviceTemplate device)
        {
            _deviceViewModel.Current = device;
            _deviceViewModel.ParamPanel.SetDescriptors(device?.Factory as IParameterPresentAdapter, AsGroup(device?.Factory.GetParameters(device.Clz)));

            InvalidateScrollInfo();
            IsLayoutDirty = IsLoaded;
        }

        private void InitializeConsumerConfigurationPanel(ConsumerConfigViewModel consumerConfigViewModel, ConsumerTemplate consumer)
        {
            consumerConfigViewModel.Current = consumer;
            consumerConfigViewModel.ParamPanel.SetDescriptors(consumer?.Factory as IParameterPresentAdapter, AsGroup(consumer?.Factory.GetParameters(consumer.Clz)));

            InvalidateScrollInfo();
            IsLayoutDirty = IsLoaded;
        }

        private void ConfigurationPanel_OnLayoutChanged(object sender, LayoutChangedEventArgs e)
        {
            if (e.IsInitialization) InvalidateScrollInfo();
            IsLayoutDirty = true;
        }

        private void DeviceComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (ComboBox)sender;
            var viewModel = (DeviceConfigViewModel)comboBox.Tag;
            var device = comboBox.SelectedItem as DeviceTemplate;
            var oldDevice = viewModel.Current;
            var oldContext = viewModel.ParamPanel.Context;
            InitializeDeviceConfigurationPanel(device);

            if (_deviceUpdateLock.IsReferred) return;
            var eventArgs = new DeviceChangedEventArgs(_deviceType, oldDevice, device, oldContext);
            DeviceChanged?.Invoke(this, eventArgs);
            viewModel.ParamPanel.Context = eventArgs.NewDeviceArgs ?? EmptyContext.Instance;
        }

        private void ConsumerComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (ComboBox) sender;
            var viewModel = (ConsumerConfigViewModel) comboBox.Tag;
            var consumer = comboBox.SelectedItem as ConsumerTemplate;
            var oldConsumer = viewModel.Current;
            var oldContext = viewModel.ParamPanel.Context;
            InitializeConsumerConfigurationPanel(viewModel, consumer);

            if (_consumerUpdateLock.IsReferred) return;
            var eventArgs = new ConsumerChangedEventArgs(_deviceType, oldConsumer, consumer, oldContext);
            ConsumerChanged?.Invoke(this, eventArgs);
            viewModel.ParamPanel.Context = eventArgs.NewConsumerArgs ?? EmptyContext.Instance;
        }

        private void AppendConsumerButton_OnClick(object sender, RoutedEventArgs e) => AppendConsumerConfig();

    }
}
