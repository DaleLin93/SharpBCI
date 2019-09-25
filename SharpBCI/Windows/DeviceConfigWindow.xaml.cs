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

    public class ConsumerChangedEventArgs : EventArgs
    {

        public readonly PluginStreamConsumer OldConsumer, NewConsumer;

        public readonly IReadonlyContext OldConsumerParams;

        public IReadonlyContext NewConsumerParams;

        public ConsumerChangedEventArgs(PluginStreamConsumer oldConsumer, PluginStreamConsumer newConsumer, IReadonlyContext oldConsumerParams)
        {
            OldConsumer = oldConsumer;
            NewConsumer = newConsumer;
            OldConsumerParams = oldConsumerParams;
        }

    }

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for ParameterizedConfigWindow.xaml
    /// </summary>
    public partial class DeviceConfigWindow
    {

        private class ConsumerConfigViewModel
        {

            internal readonly StackPanel Container;

            internal readonly ComboBox ComboBox;

            internal readonly ParameterPanel ParamPanel;
            
            internal PluginStreamConsumer CurrentConsumer;

            public ConsumerConfigViewModel(IEnumerable consumers)
            {
                Container = new StackPanel();
                var consumerGroupPanel = Container.AddGroupPanel("Consumer", "Consumer Selection");
                ComboBox = new ComboBox {ItemsSource = consumers, Tag = this};
                consumerGroupPanel.AddRow("Consumer", ComboBox);
                ParamPanel = new ParameterPanel {AllowCollapse = false, Tag = this};
                Container.Children.Add(ParamPanel);
            }

        }

        public const string NoneIdentifier = "<NONE>";

        public event EventHandler<ConsumerChangedEventArgs> ConsumerChanged;

        private readonly PluginDevice _device;

        private readonly LinkedList<ConsumerConfigViewModel> _configViewModels = new LinkedList<ConsumerConfigViewModel>();

        private bool _needResizeWindow;

        public DeviceConfigWindow([NotNull] PluginDevice device, [CanBeNull] IReadonlyContext deviceParams,
            [CanBeNull] IReadOnlyCollection<Tuple<PluginStreamConsumer, IReadonlyContext>> consumers)
        {
            InitializeComponent();

            _device = device;

            InitializeDeviceConfigurationPanel(device ?? throw new ArgumentNullException(nameof(device)));
            DeviceConfigurationPanel.Context = deviceParams ?? EmptyContext.Instance;

            if (consumers != null)
                foreach (var consumer in consumers)
                {
                    if (string.IsNullOrWhiteSpace(consumer.Item1?.Identifier)) continue;
                    var viewModel = AddConsumerConfig();
                    viewModel.ComboBox.FindAndSelect(consumer.Item1.Identifier, 0);
                    InitializeConsumerConfigurationPanel(viewModel, consumer.Item1);
                    viewModel.ParamPanel.Context = consumer.Item2 ?? EmptyContext.Instance;
                }

            if (!_configViewModels.Any()) AddConsumerConfig();

            Title = $"{device.Identifier} Configuration";
        }

        private ConsumerConfigViewModel AddConsumerConfig()
        {
            var viewModel = new ConsumerConfigViewModel(GetConsumerList(_device.DeviceType));
            viewModel.ComboBox.SelectionChanged += ConsumerComboBox_OnSelectionChanged;
            viewModel.ComboBox.SelectedIndex = 0;
            viewModel.ParamPanel.ContextChanged += ConfigurationPanel_OnContextChanged;
            viewModel.ParamPanel.LayoutChanged += ConfigurationPanel_OnLayoutChanged;
            _configViewModels.AddLast(viewModel);
            ConsumersStackPanel.Children.Add(viewModel.Container);
            return viewModel;
        }

        private static IEnumerable<IDescriptor> AsGroup(string name, IEnumerable<IDescriptor> @params)
        {
            if (@params == null) return EmptyArray<IDescriptor>.Instance;
            var array = @params.ToArray();
            return array.Length == 0 ? array : new IDescriptor[] {new ParameterGroup(name, array) };
        }

        private static IList GetConsumerList(DeviceType deviceType)
        {
            var streamerValueType = deviceType.StreamerFactory?.ValueType;
            if (streamerValueType == null) return new object[] { NoneIdentifier };
            var list = new List<object> {NoneIdentifier};
            list.AddRange(App.Instance.Registries.Registry<PluginStreamConsumer>().Registered
                .Where(pc => pc.Factory.GetAcceptType(pc.ConsumerClass).IsAssignableFrom(streamerValueType)));
            return list;
        }

        public bool ShowDialog([NotNull] out IReadonlyContext deviceParams, [NotNull] out IReadOnlyCollection<Tuple<PluginStreamConsumer, IReadonlyContext>> consumers)
        {
            deviceParams = EmptyContext.Instance;
            consumers = EmptyArray<Tuple<PluginStreamConsumer, IReadonlyContext>>.Instance;
            var dialogResult = ShowDialog() ?? false;
            if (!dialogResult) return false;
            deviceParams = DeviceConfigurationPanel.Context;
            var consumerList = new List<Tuple<PluginStreamConsumer, IReadonlyContext>>(_configViewModels.Count);
            consumerList.AddRange(from viewModel in _configViewModels where viewModel.CurrentConsumer != null 
                select new Tuple<PluginStreamConsumer, IReadonlyContext>(viewModel.CurrentConsumer, viewModel.ParamPanel.Context));
            consumers = consumerList;
            return true;
        }

        private void InitializeDeviceConfigurationPanel(PluginDevice device)
        {
            DeviceConfigurationPanel.SetDescriptors(device?.Factory as IParameterPresentAdapter, AsGroup("Device", device?.Factory.GetParameters(device.DeviceClass)));

            ScrollView.InvalidateScrollInfo();
            ScrollView.ScrollToTop();
            _needResizeWindow = true;
        }

        private void InitializeConsumerConfigurationPanel(ConsumerConfigViewModel consumerConfigViewModel, PluginStreamConsumer consumer)
        {
            consumerConfigViewModel.ParamPanel.SetDescriptors(consumer?.Factory as IParameterPresentAdapter, consumer?.Factory.GetParameters(consumer.ConsumerClass));

            consumerConfigViewModel.CurrentConsumer = consumer;
            ScrollView.InvalidateScrollInfo();
            ScrollView.ScrollToTop();
            _needResizeWindow = true;
        }

        private void Confirm()
        {
            var deviceInvalidParams = DeviceConfigurationPanel.GetInvalidParams().ToArray();
            var consumerInvalidParamsArray = _configViewModels.Select(vm => vm.ParamPanel.GetInvalidParams().ToArray()).ToArray();
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
            if (!IsVisible || !_needResizeWindow) return;
            var point = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
            var screen = System.Windows.Forms.Screen.FromPoint(point.RoundToSdPoint());
            var scaleFactor = GraphicsUtils.Scale;
            var maxHeight = screen.WorkingArea.Height / scaleFactor;
            var contentHeight = StackPanel.Children.OfType<FrameworkElement>().Sum(el => el.ActualHeight);
            Height = Math.Min(contentHeight + 50 + (ActualHeight - ScrollView.ActualHeight), maxHeight);
            var offset = screen.WorkingArea.Bottom / scaleFactor - (Top + ActualHeight);
            if (offset < 0) Top += offset;
            _needResizeWindow = false;
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyStates == Keyboard.GetKeyStates(Key.Return) && Keyboard.Modifiers == ModifierKeys.Alt) Confirm();
        }

        private void ConfigurationPanel_OnLayoutChanged(object sender, LayoutChangedEventArgs e)
        {
            if (e.IsInitialization)
            {
                ScrollView.InvalidateScrollInfo();
                ScrollView.ScrollToTop();
            }
            _needResizeWindow = true;
        }

        private void ConfigurationPanel_OnContextChanged(object sender, ContextChangedEventArgs e) { }

        private void ConsumerComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = (ComboBox) sender;
            var viewModel = (ConsumerConfigViewModel) comboBox.Tag;
            var streamConsumer = comboBox.SelectedItem as PluginStreamConsumer;

            var eventArgs = new ConsumerChangedEventArgs(viewModel.CurrentConsumer, streamConsumer, viewModel.ParamPanel.Context);
            ConsumerChanged?.Invoke(this, eventArgs);

            InitializeConsumerConfigurationPanel(viewModel, streamConsumer);
            viewModel.ParamPanel.Context = eventArgs.NewConsumerParams ?? EmptyContext.Instance;
        }

        private void AddConsumerButton_OnClick(object sender, RoutedEventArgs e) => AddConsumerConfig();

        private void OkBtn_Click(object sender, RoutedEventArgs e) => Confirm();

    }
}
