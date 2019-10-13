using MarukoLib.Lang;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using JetBrains.Annotations;
using MarukoLib.Interop;
using MarukoLib.IO;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Extensions;
using SharpBCI.Plugins;
using SharpBCI.Extensions.Windows;
using MarukoLib.Logging;
using MarukoLib.Persistence;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using ValidationResult = SharpBCI.Extensions.ValidationResult;

namespace SharpBCI.Windows
{

    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    partial class LauncherWindow : Bootstrap.ISessionListener
    {

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        internal class WindowConfig
        {

            public const string CurrentVersion = "0.1";

            public const int MaxRecentSessionCount = 10;

            public string Version = CurrentVersion;

            public string Subject;

            public string SessionName;

            public string SelectedParadigm;

            public IDictionary<string, string> SelectedDevices = new Dictionary<string, string>();

            public IDictionary<string, string[]> SelectedConsumers = new Dictionary<string, string[]>();

            public IList<ParameterizedEntity> Paradigms = new List<ParameterizedEntity>();

            public IList<ParameterizedEntity> Devices = new List<ParameterizedEntity>();

            public IList<ParameterizedEntity> Consumers = new List<ParameterizedEntity>();

            public LinkedList<string> RecentSessions = new LinkedList<string>();

            private static void Set(IList<ParameterizedEntity> list, ParameterizedEntity value)
            {
                for (var i = 0; i < list.Count; i++)
                    if (Equals(list[i].Id, value.Id))
                    {
                        list[i] = value;
                        return;
                    }
                list.Add(value);
            }

            public ParameterizedEntity GetParadigm(string name) => Paradigms.FirstOrDefault(entity => Equals(entity.Id, name));

            public ParameterizedEntity GetDevice( string name) => Devices.FirstOrDefault(entity => Equals(entity.Id, name));

            public ParameterizedEntity GetConsumer(string name) => Consumers.FirstOrDefault(entity => Equals(entity.Id, name));

            public void SetParadigm(ParameterizedEntity entity) => Set(Paradigms, entity);

            public void SetDevice(ParameterizedEntity entity) => Set(Devices, entity);

            public void SetConsumer(ParameterizedEntity entity) => Set(Consumers, entity);

            public void AddRecentSession(string prefix)
            {
                if (RecentSessions == null) RecentSessions = new LinkedList<string>();
                RecentSessions.AddFirst(prefix);
                while (RecentSessions.Count > MaxRecentSessionCount) RecentSessions.RemoveLast();
            }

        }

        private struct ParamValidationResult
        {

            public readonly IParameterDescriptor Param;

            public readonly ValidationResult Result;

            public ParamValidationResult(IParameterDescriptor param, ValidationResult result)
            {
                Param = param;
                Result = result;
            }

        }

        private const string ConfigDir = "Config\\";

        private const string DataDir = App.DataDir + "\\";

        private const string ConfigFile = "config.json";

        private static readonly Logger Logger = Logger.GetLogger(typeof(LauncherWindow));

        /* UI */

        private TextBox _subjectTextBox, _sessionDescriptorTextBox;

        private Grid _paradigmDescriptionRow;

        private TextBlock _sessionFullNameTextBlock, _paradigmDescriptionTextBlock;

        private ComboBox _paradigmComboBox;

        private DeviceSelectionPanel _deviceConfigPanel;

        private bool _needResizeWindow;

        /* Config */

        private WindowConfig _config = new WindowConfig();

        /* Temporary variables */

        private PluginParadigm _currentParadigm;

        static LauncherWindow() => Directory.CreateDirectory(ConfigDir);

        public LauncherWindow()
        {
            if (App.IsRealtimePriority) Title += " (Realtime)"; 
            InitializeComponent();
            InitializeHeaderPanel();
            InitializeFooterPanel();
        }

        public void SetErrorMessage([CanBeNull] string errorMessage)
        {
            if (errorMessage?.IsBlank() ?? true)
                ErrorMsgContainer.Visibility = Visibility.Hidden;
            else
            {
                ErrorMsgContainer.Visibility = Visibility.Visible;
                ErrorMsgTextBox.Text = errorMessage;
            }
        }

        public void StartSession()
        {
            if (_subjectTextBox.Text.IsBlank())
            {
                MessageBox.Show("'Subject' cannot be empty.", "SharpBCI");
                return;
            }
            if (_sessionDescriptorTextBox.Text.IsBlank())
            {
                MessageBox.Show("'Session Name' cannot be empty.", "SharpBCI");
                return;
            }
            try
            {
                lock (StartBtn)
                {
                    if (!ValidateParadigmParams()) return;
                    if (!StartBtn.IsEnabled) return;
                    StartBtn.IsEnabled = false;
                    Visibility = Visibility.Hidden;
                }
                Bootstrap.StartSession(GetSessionConfig(), MonitorWindow.IsShown, this);
            }
            catch (Exception ex)
            {
                Logger.Error("StartSession", ex);
                App.ShowErrorMessage(ex);
            }
            finally
            {
                lock (StartBtn)
                {
                    StartBtn.IsEnabled = true;
                    Visibility = Visibility.Visible;
                }
            }
        }

        public SessionConfig GetSessionConfig()
        {
            var paradigm = (PluginParadigm)_paradigmComboBox.SelectedItem ?? throw new UserException("No paradigm was selected");
            var paradigmEntity = new ParameterizedEntity(paradigm.Identifier, paradigm.ParadigmAttribute.Version?.ToString(), paradigm.SerializeParams(ParadigmParamPanel.Context));
            return new SessionConfig
            {
                Subject = _subjectTextBox.Text,
                SessionDescriptor = _sessionDescriptorTextBox.Text,
                Paradigm = paradigmEntity,
                Devices = _deviceConfigPanel.DeviceConfig,
                Monitor = MonitorWindow.IsShown
            };
        }

        public void SetSessionConfig(SessionConfig config)
        {
            _subjectTextBox.Text = config.Subject ?? _subjectTextBox.Text;
            _sessionDescriptorTextBox.Text = config.SessionDescriptor ?? _sessionDescriptorTextBox.Text;
            if (_paradigmComboBox.FindAndSelectFirstByString(config.Paradigm.Id, null))
                ParadigmParamPanel.Context = _currentParadigm.DeserializeParams(config.Paradigm.Params);
            _deviceConfigPanel.DeviceConfig = config.Devices;
        }

        public void LoadConfig()
        {
            _config = JsonUtils.DeserializeFromFile<WindowConfig>(ConfigFile) ?? new WindowConfig();

            _subjectTextBox.Text = _config.Subject ?? _subjectTextBox.Text;
            _sessionDescriptorTextBox.Text = _config.SessionName ?? _sessionDescriptorTextBox.Text;
            _paradigmComboBox.FindAndSelectFirstByString(_config.SelectedParadigm, 0);

            DeserializeParadigmConfig();
            DeserializeDevicesConfig();
        }

        public void SaveConfig()
        {
            _config.Subject = _subjectTextBox.Text;
            _config.SessionName = _sessionDescriptorTextBox.Text;
            _config.SelectedParadigm = (_paradigmComboBox.SelectedItem as PluginParadigm)?.Identifier;

            SerializeParadigmConfig();
            SerializeDevicesConfig();

            _config.JsonSerializeToFile(ConfigFile, JsonUtils.PrettyFormat);
        }

        private void UpdateFullSessionName(PluginParadigm paradigm, IReadonlyContext @params)
        {
            if (paradigm != null)
            {
                try
                {
                    _sessionFullNameTextBlock.Foreground = Brushes.DarkGray;
                    _sessionFullNameTextBlock.Text = SessionConfigExt.GetFullSessionName(_subjectTextBox.Text, _sessionDescriptorTextBox.Text, @params);
                    return;
                }
                catch (Exception e)
                {
                    Logger.Warn("UpdateFullSessionName - update full session name", e, "sessionDescriptor", _sessionDescriptorTextBox.Text);
                }
            }
            _sessionFullNameTextBlock.Text = "<ERR>";
            _sessionFullNameTextBlock.Foreground = Brushes.DarkRed;
        }

        private void OnParadigmParamsUpdated()
        {
            var pluginParadigm = _currentParadigm;
            if (pluginParadigm == null) return;
            if (!ValidateParadigmParams(false)) return;

            var context = ParadigmParamPanel.Context;
            UpdateFullSessionName(pluginParadigm, context);
            Bootstrap.TryInitiateParadigm(pluginParadigm, context, out var paradigm, false);
            ParadigmSummaryPanel.Update(context, paradigm);
        }

        private void InitializeHeaderPanel()
        {
            var sessionPanel = HeaderPanel.AddGroupPanel("Session", "General Session Information");
            sessionPanel.AddRow("Subject", _subjectTextBox = new TextBox { Text = "Anonymous", MaxLength = 32});
            sessionPanel.AddRow("Session Descriptor", _sessionDescriptorTextBox = new TextBox { Text = "Unnamed", MaxLength = 64 });
            sessionPanel.AddRow("", _sessionFullNameTextBlock = new TextBlock {FontSize = 10, Foreground = Brushes.DarkGray, Margin = new Thickness {Top = 3}});
            void OnSessionInfoChanged(object sender, TextChangedEventArgs e) => UpdateFullSessionName(_currentParadigm, ParadigmParamPanel.Context);
            _subjectTextBox.TextChanged += OnSessionInfoChanged;
            _sessionDescriptorTextBox.TextChanged += OnSessionInfoChanged;
            var paradigmPanel = HeaderPanel.AddGroupPanel("Paradigm", "Paradigm Selection");
            var paradigmComboBoxContainer = new Grid();
            _paradigmComboBox = new ComboBox {Margin = new Thickness {Right = ViewConstants.DefaultRowHeight + ViewConstants.MinorSpacing}};
            _paradigmComboBox.SelectionChanged += ParadigmComboBox_OnSelectionChanged;
            paradigmComboBoxContainer.Children.Add(_paradigmComboBox);
            var loadDefaultCfgBtnImageSource = new BitmapImage(new Uri(ViewConstants.ResetImageUri, UriKind.Absolute));
            var loadDefaultCfgImage = new Image {Margin = new Thickness(2), Source = loadDefaultCfgBtnImageSource};
            var loadDefaultCfgBtn = new Button {ToolTip = "Load Default Config", HorizontalAlignment = HorizontalAlignment.Right, Width = ViewConstants.DefaultRowHeight, Content = loadDefaultCfgImage};
            loadDefaultCfgBtn.Click += ParadigmResetBtn_OnClick;
            paradigmComboBoxContainer.Children.Add(loadDefaultCfgBtn);
            paradigmPanel.AddRow("Paradigm", paradigmComboBoxContainer);
            _paradigmDescriptionRow = paradigmPanel.AddRow("", _paradigmDescriptionTextBlock = new TextBlock {FontSize = 10, Foreground = Brushes.DarkGray});
            _paradigmDescriptionRow.Visibility = Visibility.Collapsed;
        }

        private void InitializeFooterPanel()
        {
            _deviceConfigPanel = new DeviceSelectionPanel();
            _deviceConfigPanel.DeviceChanged += (sender, e) =>
            {
                SerializeDeviceConfig(e.OldDevice, e.OldDeviceParams);
                e.NewDeviceParams = DeserializeDeviceConfig(e.NewDevice);
            };
            _deviceConfigPanel.ConsumerChanged += (sender, e) =>
            {
                SerializeConsumerConfig(e.OldConsumer, e.OldConsumerParams);
                e.NewConsumerParams = DeserializeConsumerConfig(e.NewConsumer);
            };
            FooterPanel.Children.Add(_deviceConfigPanel);
        }

        private void InitializeParadigmConfigurationPanel(PluginParadigm paradigm)
        {
            _paradigmDescriptionTextBlock.Text = paradigm.ParadigmAttribute.Description;
            _paradigmDescriptionRow.Visibility = string.IsNullOrWhiteSpace(_paradigmDescriptionTextBlock.Text) 
                ? Visibility.Collapsed : Visibility.Visible;

            ParadigmParamPanel.SetDescriptors(paradigm.Factory as IParameterPresentAdapter, paradigm.Factory.GetParameterGroups(paradigm.ParadigmClass));
            ParadigmSummaryPanel.SetSummaries(paradigm.Factory as ISummaryPresentAdapter, paradigm.Factory.GetSummaries(paradigm.ParadigmClass));

            ScrollView.InvalidateScrollInfo();
            ScrollView.ScrollToTop();

            _currentParadigm = paradigm;
            OnParadigmParamsUpdated();
            _needResizeWindow = true;
        }
        
        private void SerializeParadigmConfig()
        {
            var paradigm = _currentParadigm;
            if (paradigm == null) return;
            _config.SetParadigm(new ParameterizedEntity(paradigm.Identifier, 
                paradigm.ParadigmAttribute.Version?.ToString(), 
                paradigm.SerializeParams(ParadigmParamPanel.Context)));
        }

        private void DeserializeParadigmConfig()
        {
            var paradigm = _currentParadigm;
            if (paradigm == null) return;
            var entity = _config.GetParadigm(paradigm.Identifier);
            ParadigmParamPanel.Context = (IReadonlyContext) paradigm.DeserializeParams(entity.Params) ?? EmptyContext.Instance;
        }

        private void SerializeDevicesConfig()
        {
            foreach (var deviceType in _deviceConfigPanel.DeviceTypes)
            {
                var deviceParams = _deviceConfigPanel[deviceType];
                _config.SelectedDevices[deviceType.Name] = deviceParams.Device.Id;
                _config.SetDevice(deviceParams.Device);
                _config.SelectedConsumers[deviceType.Name] = deviceParams.Consumers.Select(c => c.Id).ToArray();
                foreach (var consumerEntity in deviceParams.Consumers)
                    _config.SetConsumer(consumerEntity);
            }

        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        private void SerializeDeviceConfig(PluginDevice device, IReadonlyContext @params)
        {
            if (device == null) return;
            _config.SetDevice(new ParameterizedEntity(device.Identifier, device.SerializeParams(@params)));
        }

        private void DeserializeDevicesConfig()
        {
            foreach (var deviceType in _deviceConfigPanel.DeviceTypes)
                _deviceConfigPanel[deviceType] = new DeviceParams
                {
                    DeviceType = deviceType.Name,
                    Device = _config.SelectedDevices.TryGetValue(deviceType.Name, out var did) ? _config.GetDevice(did) : default,
                    Consumers = _config.SelectedConsumers.TryGetValue(deviceType.Name, out var consumerIds) && consumerIds != null 
                        ? consumerIds.Select(cid => _config.GetConsumer(cid)).ToArray() : EmptyArray<ParameterizedEntity>.Instance
                };
        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        private IReadonlyContext DeserializeDeviceConfig(PluginDevice device) =>
            device == null ? EmptyContext.Instance : (IReadonlyContext)device.DeserializeParams(_config.GetDevice(device.Identifier).Params) ?? EmptyContext.Instance;

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        private void SerializeConsumerConfig(PluginStreamConsumer consumer, IReadonlyContext @params)
        {
            if (consumer == null) return;
            _config.SetConsumer(new ParameterizedEntity(consumer.Identifier, consumer.SerializeParams(@params)));
        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        private IReadonlyContext DeserializeConsumerConfig(PluginStreamConsumer consumer) =>
            consumer == null ? EmptyContext.Instance : (IReadonlyContext)consumer.DeserializeParams(_config.GetConsumer(consumer.Identifier).Params) ?? EmptyContext.Instance;

        private bool ValidateParadigmParams(bool msgBox = true)
        {
            var paradigm = _currentParadigm;
            var factory = paradigm?.Factory;
            if (factory == null) return false;
            var adapter = factory as IParameterPresentAdapter;
            var invalidParamValidationResults = ParadigmParamPanel.GetInvalidParams()
                .Select(p => new ParamValidationResult(p, ValidationResult.Failed(null)))
                .ToList();
            if (invalidParamValidationResults.Count <= 0)
            {
                var context = ParadigmParamPanel.Context;
                invalidParamValidationResults = context.Properties
                    .Where(cp => cp is IParameterDescriptor pd && (adapter?.IsVisible(context, pd) ?? true))
                    .Select(cp => (IParameterDescriptor)cp)
                    .Select(pd =>
                    {
                        var valid = ValidationResult.Failed();
                        try { valid = factory.CheckValid(paradigm.ParadigmClass, context, pd); }
                        catch (Exception e) { Logger.Warn("ValidateParadigmParams", e, "parameter", pd.Key); }
                        var row = ParadigmParamPanel[pd]?.Container;
                        if (row != null && (row.IsError = valid.IsFailed)) row.ErrorMessage = valid.Message.Trim();
                        return new ParamValidationResult(pd, valid);
                    })
                    .Where(result => result.Result.IsFailed)
                    .ToList();
                if (invalidParamValidationResults.Count <= 0)
                {
                    SetErrorMessage(null);
                    return true;
                }
            }
            var stringBuilder = new StringBuilder("The following parameters of paradigm are invalid");
            foreach (var paramValidationResult in invalidParamValidationResults)
            {
                stringBuilder.Append("\n - ").Append(paramValidationResult.Param.Name);
                if (paramValidationResult.Result.Message != null)
                    stringBuilder.Append(" : ").Append(paramValidationResult.Result.Message);
            }
            var errorMessage = stringBuilder.ToString();
            SetErrorMessage(errorMessage);
            if (msgBox) MessageBox.Show(errorMessage); 
            return false;
        }

        public void LoadPlatformCaps()
        {
            var style = (Style)FindResource("MenuItem");
            var desktop = GraphicsUtils.DesktopHdc;
            var capMenuItems = new LinkedList<MenuItem>();
            foreach (var deviceCap in typeof(Gdi32.DeviceCap).GetEnumValues())
            {
                var header = $"{typeof(Gdi32.DeviceCap).GetEnumName(deviceCap)}: {Gdi32.GetDeviceCaps(desktop, (int) deviceCap)}";
                capMenuItems.AddLast(new MenuItem {Style = style, Header = header});
            }
            PlatformCapsMenuItem.ItemsSource = capMenuItems.ToArray();
        }

        private void RefreshRecentSessionMenuItems()
        {
            var style = (Style) FindResource("MenuItem");
            var menuItems = new LinkedList<MenuItem>();
            if (_config.RecentSessions?.IsEmpty() ?? true)
                menuItems.AddLast(new MenuItem {Style = style, Header = "None", IsEnabled = false});
            else
                foreach (var recentSession in _config.RecentSessions)
                {
                    var menuItem = new MenuItem {Style = style, Header = recentSession};
                    menuItem.Click += (sender, e) => SetSessionConfig(JsonUtils.DeserializeFromFile<SessionConfig>(recentSession + SessionConfig.FileSuffix));
                    menuItems.AddLast(menuItem);
                }
            LoadFromRecentSessionMenuItem.ItemsSource = menuItems.ToArray();
        }

        private void RefreshAppMenuItems()
        {
            var style = (Style)FindResource("MenuItem");
            var menuItems = new LinkedList<object>();
            var appEntries = App.Instance.Registries.Registry<PluginAppEntry>().Registered;
            if (appEntries?.IsEmpty() ?? true)
                menuItems.AddLast(new MenuItem { Style = style, Header = "None", IsEnabled = false });
            else
                foreach (var appEntry in appEntries)
                {
                    var appEntryMenuItem = new MenuItem { Style = style, Header = $"{appEntry.Identifier} - ({appEntry.Plugin?.Name ?? "Embedded"})" };
                    appEntryMenuItem.Click += (sender, e) => appEntry.Entry.Run();
                    menuItems.AddLast(appEntryMenuItem);
                }
            AppsMenuItem.ItemsSource = menuItems.ToArray();
        }

        private void RefreshPluginMenuItems()
        {
            var style = (Style)FindResource("MenuItem");
            var menuItems = new LinkedList<object>();
            var plugins = App.Instance.Registries.Registry<Plugin>().Registered;
            if (plugins?.IsEmpty() ?? true)
                menuItems.AddLast(new MenuItem { Style = style, Header = "None", IsEnabled = false});
            else
                foreach (var plugin in plugins)
                {
                    var menuItem = new MenuItem { Style = style, Header = plugin.Name};
                    var children = new LinkedList<object>();

                    foreach (var deviceType in _deviceConfigPanel.DeviceTypes)
                    {
                        var devices = plugin.Devices.Where(d => d.DeviceType == deviceType).ToArray();
                        if (devices.Length <= 0)
                            children.AddLast(new MenuItem { Style = style, Header = $"No {deviceType.DisplayName.ToLowerInvariant()} Implementations", IsEnabled = false });
                        else
                            foreach (var device in devices)
                            {
                                var deviceAttribute = device.DeviceAttribute;
                                var menuItemHeader = $"{deviceAttribute.Name} ({deviceAttribute.FullVersionName}) - {device.DeviceClass.FullName}";
                                var deviceMenuItem = new MenuItem { Style = style, Header = menuItemHeader };
                                deviceMenuItem.Click += (sender, e) => _deviceConfigPanel.FindAndSelectDevice(deviceType, device.DeviceName, null);
                                children.AddLast(deviceMenuItem);
                            }
                        children.AddLast(new Separator());
                    }

                    if (!plugin.AppEntries.Any())
                        children.AddLast(new MenuItem { Style = style, Header = "No App Entry Implementations", IsEnabled = false });
                    else
                        foreach (var appEntry in plugin.AppEntries)
                        {
                            var appEntryMenuItem = new MenuItem { Style = style, Header = $"{appEntry.Identifier} - {appEntry.GetType().FullName}" };
                            appEntryMenuItem.Click += (sender, e) => appEntry.Entry.Run();
                            children.AddLast(appEntryMenuItem);
                        }
                    children.AddLast(new Separator());

                    if (!plugin.Paradigms.Any())
                        children.AddLast(new MenuItem { Style = style, Header = "No Paradigm Implementations", IsEnabled = false });
                    else
                        foreach (var paradigm in plugin.Paradigms)
                        {
                            var paradigmAttribute = paradigm.ParadigmAttribute;
                            var menuItemHeader = $"{paradigmAttribute.Name} ({paradigmAttribute.FullVersionName}) - {paradigm.ParadigmClass.FullName}";
                            var paradigmMenuItem = new MenuItem { Style = style, Header = menuItemHeader };
                            paradigmMenuItem.Click += (sender, e) => _paradigmComboBox.FindAndSelectFirstByString(paradigmAttribute.Name, null);
                            children.AddLast(paradigmMenuItem);
                        }
                    children.AddLast(new Separator());

                    if (!plugin.StreamConsumers.Any())
                        children.AddLast(new MenuItem { Style = style, Header = "No Stream-Consumer Implementations", IsEnabled = false });
                    else
                        foreach (var streamConsumer in plugin.StreamConsumers)
                        {
                            var consumerAttribute = streamConsumer.ConsumerAttribute;
                            var menuItemHeader = $"{consumerAttribute.Name} ({consumerAttribute.FullVersionName}) - {streamConsumer.ConsumerClass.FullName}";
                            var consumerMenuItem = new MenuItem { Style = style, Header = menuItemHeader };
                            consumerMenuItem.Click += (sender, e) => _paradigmComboBox.FindAndSelectFirstByString(consumerAttribute.Name, null);
                            children.AddLast(consumerMenuItem);
                        }
                    children.AddLast(new Separator());

                    if (!plugin.Paradigms.Any() && !plugin.CustomMarkers.Any())
                        children.AddLast(new MenuItem { Style = style, Header = "No Custom Marker Definitions", IsEnabled = false });
                    else 
                        foreach (var keyValuePair in plugin.CustomMarkers.OrderBy(pair => pair.Key))
                            children.AddLast(new MenuItem { Style = style, Header = $"{keyValuePair.Key} - {keyValuePair.Value}"});
                    menuItem.ItemsSource = children;
                    menuItems.AddLast(menuItem);
                }
            PluginsMenuItem.ItemsSource = menuItems.ToArray();
        }

        private void Window_OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadPlatformCaps();

            _paradigmComboBox.ItemsSource = App.Instance.Registries.Registry<PluginParadigm>().Registered.OrderBy(p => p.Identifier);
            _deviceConfigPanel.UpdateDevices();

            LoadConfig();

            RefreshRecentSessionMenuItems();
            RefreshAppMenuItems();
            RefreshPluginMenuItems();
        }

        private void Window_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyStates == Keyboard.GetKeyStates(Key.Return) && Keyboard.Modifiers == ModifierKeys.Alt) StartSession();
        }

        private void Window_OnClosed(object sender, EventArgs e) => App.Kill();

        private void Window_OnLayoutUpdated(object sender, EventArgs e)
        {
            if (!IsVisible || !_needResizeWindow || !IsLoaded) return;
            var contentHeight = MainPanel.Children.OfType<FrameworkElement>().Sum(el => el.ActualHeight);
            var minWidth = (_currentParadigm?.Factory as IPresentAdapter)?.DesiredWidth ?? double.NaN;
            this.UpdateWindowSize(contentHeight + 50 + (ActualHeight - ScrollView.ActualHeight), minWidth);
            _needResizeWindow = false;
        }

        private void ParadigmComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SerializeParadigmConfig();
            var paradigm = (PluginParadigm) _paradigmComboBox.SelectedItem;
            InitializeParadigmConfigurationPanel(paradigm);
            DeserializeParadigmConfig();
        }

        private void ParadigmResetBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var paradigm = _currentParadigm;
            if (paradigm == null) return;
            ParadigmParamPanel.ResetToDefault();
            OnParadigmParamsUpdated();
        }

        private void SaveMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ValidateParadigmParams()) return;
            SaveConfig();
        }

        private void ReloadMenuItem_OnClick(object sender, RoutedEventArgs e) => LoadConfig();

        private void NewMultiSessionConfigMenuItem_OnClick(object sender, RoutedEventArgs e) => new MultiSessionConfigWindow(null).ShowDialog();

        private void OpenMultiSessionConfigMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Config File",
                Multiselect = false,
                CheckFileExists = true,
                DefaultExt = MultiSessionConfig.FileSuffix,
                Filter = FileUtils.GetFileFilter("Multi-Session Config File", MultiSessionConfig.FileSuffix),
                InitialDirectory = Path.GetFullPath(ConfigDir)
            };
            if (!dialog.ShowDialog(this).Value) return;
            new MultiSessionConfigWindow(dialog.FileName).ShowDialog();
        }

        private void SaveAsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var sessionConfig = GetSessionConfig();
            var defaultFileName = sessionConfig.GetFullSessionName().RemoveInvalidCharacterForFileName() + SessionConfig.FileSuffix;
            var dialog = new SaveFileDialog
            {
                Title = "Save Config File",
                OverwritePrompt = true,
                AddExtension = true,
                FileName = defaultFileName,
                DefaultExt = SessionConfig.FileSuffix,
                Filter = FileUtils.GetFileFilter("Session Config File", SessionConfig.FileSuffix),
                InitialDirectory = Path.GetFullPath(ConfigDir)
            };
            if (!dialog.ShowDialog(this).Value) return;
            sessionConfig.JsonSerializeToFile(dialog.FileName, JsonUtils.PrettyFormat);
        }

        private void LoadFromMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Config File",
                Multiselect = false,
                CheckFileExists = true,
                DefaultExt = SessionConfig.FileSuffix,
                Filter = FileUtils.GetFileFilter("Session Config File" , SessionConfig.FileSuffix),
                InitialDirectory = Path.GetFullPath(ConfigDir)
            };
            if (!dialog.ShowDialog(this).Value) return;
            SetSessionConfig(JsonUtils.DeserializeFromFile<SessionConfig>(dialog.FileName));
        }

        private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e) => Close();

        private void SystemVariablesMenuItem_OnClick(object sender, RoutedEventArgs e) => App.ConfigSystemVariables();

        private void DataFolderMenuItem_OnClick(object sender, RoutedEventArgs e) => Process.Start(Path.GetFullPath(App.DataDir));

        private void MonitorMenuItem_OnClick(object sender, RoutedEventArgs e) => MonitorWindow.Show();

        private void AnalyzeMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Session File(s)",
                Multiselect = true,
                CheckFileExists = true,
                DefaultExt = "session",
                Filter = FileUtils.GetFileFilter("Session File", SessionInfo.FileSuffix),
                InitialDirectory = Path.GetFullPath(DataDir)
            };
            if (!dialog.ShowDialog(this).Value || dialog.FileNames.IsEmpty()) return;
            foreach (var fileName in dialog.FileNames)
                new AnalysisWindow(fileName.TrimEnd(Path.GetExtension(fileName))).Show();
        }

        private void ParadigmParamPanel_OnLayoutChanged(object sender, LayoutChangedEventArgs e) => _needResizeWindow = true;

        private void ParadigmParamPanel_OnContextChanged(object sender, ContextChangedEventArgs e)
        {
            if (!ValidateParadigmParams(false)) return;
            OnParadigmParamsUpdated();
        }

        private void StartBtn_OnClick(object sender, RoutedEventArgs e) => StartSession();

        void Bootstrap.ISessionListener.BeforeStart(int index, Session session)
        {
            _config.AddRecentSession(session.DataFilePrefix);
            RefreshRecentSessionMenuItems();
            SaveConfig();
        }

        void Bootstrap.ISessionListener.AfterCompleted(int index, Session session) { }

        void Bootstrap.ISessionListener.AfterAllCompleted(Session[] sessions)
        {
            foreach (var session in sessions)
                if (session?.Result != null)
                    new ResultWindow(session).Show();
        }

    }

}
