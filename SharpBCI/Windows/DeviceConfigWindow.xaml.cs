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
using SharpBCI.Registrables;

namespace SharpBCI.Windows
{

    public class ConsumerChangedEventArgs : EventArgs
    {

        public readonly RegistrableStreamConsumer OldConsumer, NewConsumer;

        public readonly IReadonlyContext OldConsumerParams;

        public IReadonlyContext NewConsumerParams;

        public ConsumerChangedEventArgs(RegistrableStreamConsumer oldConsumer, RegistrableStreamConsumer newConsumer, IReadonlyContext oldConsumerParams)
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

        public const string NoneIdentifier = "<NONE>";

        public event EventHandler<ConsumerChangedEventArgs> ConsumerChanged;

        private readonly ComboBox _consumerComboBox;

        private RegistrableStreamConsumer _currentConsumer;

        private bool _needResizeWindow;

        public DeviceConfigWindow([NotNull] RegistrableDevice device, [CanBeNull] IReadonlyContext deviceParams,
            [CanBeNull] RegistrableStreamConsumer consumer, [CanBeNull] IReadonlyContext consumerParams)
        {
            InitializeComponent();

            InitializeDeviceConfigurationPanel(device ?? throw new ArgumentNullException(nameof(device)));
            DeviceConfigurationPanel.Context = deviceParams ?? EmptyContext.Instance;

            var experimentPanel = ConsumerSelectionPanel.AddGroupPanel("Consumer", "Consumer Selection");
            _consumerComboBox = new ComboBox();
            _consumerComboBox.SelectionChanged += ConsumerComboBox_OnSelectionChanged;
            _consumerComboBox.ItemsSource = GetConsumerList(device.DeviceType);
            _consumerComboBox.FindAndSelect(consumer?.Identifier, 0);
            experimentPanel.AddRow("Consumer", _consumerComboBox, ViewConstants.DefaultRowHeight);

            InitializeConsumerConfigurationPanel(consumer);
            ConsumerConfigurationPanel.Context = consumerParams ?? EmptyContext.Instance;

            Title = $"{device.Identifier} Configuration";
        }

        private static IDescriptor[] AsGroup(string name, IDescriptor[] @params)
        {
            if (@params == null || !@params.Any()) return EmptyArray<IDescriptor>.Instance;
            return new IDescriptor[] {new ParameterGroup(name, @params)};
        }

        private static IList GetConsumerList(DeviceType deviceType)
        {
            var streamerValueType = deviceType.StreamerFactory?.ValueType;
            if (streamerValueType == null) return new object[] { NoneIdentifier };
            var list = new List<object> {NoneIdentifier};
            list.AddRange(App.Instance.Registries.Registry<RegistrableStreamConsumer>().Registered.Where(rc => rc.Factory.AcceptType.IsAssignableFrom(streamerValueType)));
            return list;
        }

        public bool ShowDialog(out IReadonlyContext deviceParams, out RegistrableStreamConsumer consumer, out IReadonlyContext consumerParams)
        {
            deviceParams = EmptyContext.Instance;
            consumer = null;
            consumerParams = EmptyContext.Instance;
            var dialogResult = ShowDialog() ?? false;
            if (!dialogResult) return false;
            deviceParams = DeviceConfigurationPanel.Context;
            consumer = _currentConsumer;
            consumerParams = ConsumerConfigurationPanel.Context;
            return true;
        }

        private void InitializeDeviceConfigurationPanel(RegistrableDevice device)
        {
            DeviceConfigurationPanel.Descriptors = AsGroup("Device", device?.Factory.Parameters.Cast<IDescriptor>().ToArray() ?? EmptyArray<IDescriptor>.Instance);
            DeviceConfigurationPanel.Adapter = device?.Factory as IParameterPresentAdapter;

            ScrollView.InvalidateScrollInfo();
            ScrollView.ScrollToTop();
            _needResizeWindow = true;
        }

        private void InitializeConsumerConfigurationPanel(RegistrableStreamConsumer consumer)
        {
            ConsumerConfigurationPanel.Descriptors = AsGroup("", consumer?.Factory.Parameters.Cast<IDescriptor>().ToArray() ?? EmptyArray<IDescriptor>.Instance);
            ConsumerConfigurationPanel.Adapter = consumer?.Factory as IParameterPresentAdapter;

            _currentConsumer = consumer;
            ScrollView.InvalidateScrollInfo();
            ScrollView.ScrollToTop();
            _needResizeWindow = true;
        }

        private void Confirm()
        {
            var deviceInvalidParams = DeviceConfigurationPanel.GetInvalidParams().ToArray();
            var consumerInvalidParams = ConsumerConfigurationPanel.GetInvalidParams().ToArray();
            if (deviceInvalidParams.Any() || consumerInvalidParams.Any())
            {
                var stringBuilder = new StringBuilder();
                stringBuilder.Append("The following parameters of device are invalid");
                foreach (var param in deviceInvalidParams) stringBuilder.Append("\n - ").Append(param.Name);
                stringBuilder.Append("\nThe following parameters of consumer are invalid");
                foreach (var param in consumerInvalidParams) stringBuilder.Append("\n - ").Append(param.Name);
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
            Height = MaxHeight = Math.Min(contentHeight + 20 + (ActualHeight - ScrollView.ActualHeight), maxHeight);
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
            var registrableConsumer = _consumerComboBox.SelectedItem as RegistrableStreamConsumer;

            var eventArgs = new ConsumerChangedEventArgs(_currentConsumer, registrableConsumer, ConsumerConfigurationPanel.Context);
            ConsumerChanged?.Invoke(this, eventArgs);

            InitializeConsumerConfigurationPanel(registrableConsumer);
            ConsumerConfigurationPanel.Context = eventArgs.NewConsumerParams ?? EmptyContext.Instance;
        }

        private void OkBtn_Click(object sender, RoutedEventArgs e) => Confirm();

    }
}
