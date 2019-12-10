using MarukoLib.Lang;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using JetBrains.Annotations;
using MarukoLib.Interop;
using MarukoLib.IO;
using MarukoLib.Lang.Concurrent;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Extensions;
using SharpBCI.Plugins;
using SharpBCI.Extensions.Windows;
using MarukoLib.Logging;
using MarukoLib.Persistence;
using MarukoLib.Threading;
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

            public string SessionDescriptor;

            public string SelectedParadigm;

            public IDictionary<string, string> SelectedDevices = new Dictionary<string, string>();

            public IDictionary<string, string[]> SelectedConsumers = new Dictionary<string, string[]>();

            public IList<SerializedObject> Paradigms = new List<SerializedObject>();

            public IList<SerializedObject> Devices = new List<SerializedObject>();

            public IList<SerializedObject> Consumers = new List<SerializedObject>();

            public LinkedList<string> RecentSessions = new LinkedList<string>();

            private static void Set(IList<SerializedObject> list, SerializedObject value)
            {
                for (var i = 0; i < list.Count; i++)
                    if (Equals(list[i].Id, value.Id))
                    {
                        list[i] = value;
                        return;
                    }
                list.Add(value);
            }

            public SerializedObject GetParadigm(string name) => Paradigms.FirstOrDefault(entity => Equals(entity.Id, name));

            public SerializedObject GetDevice( string name) => Devices.FirstOrDefault(entity => Equals(entity.Id, name));

            public SerializedObject GetConsumer(string name) => Consumers.FirstOrDefault(entity => Equals(entity.Id, name));

            public void SetParadigm(SerializedObject serializedParadigm) => Set(Paradigms, serializedParadigm);

            public void SetDevice(SerializedObject serializedDevice) => Set(Devices, serializedDevice);

            public void SetConsumer(SerializedObject serializedConsumer) => Set(Consumers, serializedConsumer);

            public void AddRecentSession(string prefix)
            {
                if (RecentSessions == null) RecentSessions = new LinkedList<string>();
                RecentSessions.AddFirst(prefix);
                while (RecentSessions.Count > MaxRecentSessionCount) RecentSessions.RemoveLast();
            }

        }

        private class ParadigmItemGroupDescription : GroupDescription
        {

            public override object GroupNameFromItem(object item, int level, CultureInfo culture) => 
                ((item as FrameworkElement)?.Tag as ParadigmTemplate)?.Category;

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

        private const int MaxSnapshotCount = 10;

        private static readonly Logger Logger = Logger.GetLogger(typeof(LauncherWindow));

        /* UI */

        private TextBox _subjectTextBox, _sessionDescriptorTextBox;

        private Grid _paradigmDescriptionRow;

        private TextBlock _sessionFullNameTextBlock, _paradigmDescriptionTextBlock;

        private ParadigmComboBox _paradigmComboBox;

        private DeviceSelectionPanel _deviceConfigPanel;

        private bool _needResizeWindow;

        /* Config */

        private readonly ReaderWriterLockSlim _configReadWriteLock;

        private readonly Timer _configAutoSaveTimer;

        private readonly AtomicBool _configDirty = new AtomicBool(false);

        private WindowConfig _config = new WindowConfig();

        /* Temporary variables */

        private ParadigmTemplate _currentParadigm;

        static LauncherWindow() => Directory.CreateDirectory(ConfigDir);

        public LauncherWindow()
        {
            _configReadWriteLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            _configAutoSaveTimer = new Timer(AutoSaveTimer_OnTick, null, 60000, 60000);
            if (App.IsRealtimePriority) Title += " (Realtime)"; 
            InitializeComponent();
            InitializeHeaderPanel();
            InitializeFooterPanel();
        }

        private static string GetConfigFilePath(uint snapshotIndex) => 
            Path.Combine(FileUtils.ExecutableDirectory, snapshotIndex == 0 ? "default.conf" : $"snapshot-{snapshotIndex + 1}.conf");

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
                    if (!CheckParadigmArgs()) return;
                    if (!StartBtn.IsEnabled) return;
                    StartBtn.IsEnabled = false;
                    Visibility = Visibility.Hidden;
                }
                Bootstrap.StartSession(GetSessionConfig(), this);
            }
            catch (Exception ex)
            {
                Logger.Error("StartSessions", ex);
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
            var paradigm = _currentParadigm ?? throw new UserException("No paradigm was selected");
            var paradigmEntity = new SerializedObject(paradigm.Identifier, paradigm.Version, paradigm.SerializeArgs(ParadigmParamPanel.Context));
            return new SessionConfig
            {
                Subject = _subjectTextBox.Text,
                SessionDescriptor = _sessionDescriptorTextBox.Text,
                Paradigm = paradigmEntity,
                Devices = _deviceConfigPanel.DeviceConfigs,
            };
        }

        public void SetSessionConfig(SessionConfig config)
        {
            _subjectTextBox.Text = config.Subject ?? _subjectTextBox.Text;
            _sessionDescriptorTextBox.Text = config.SessionDescriptor ?? _sessionDescriptorTextBox.Text;
            if (_paradigmComboBox.FindAndSelectFirstByTag(tag => Equals((tag as ParadigmTemplate)?.Identifier, config.Paradigm.Id), null))
                ParadigmParamPanel.Context = _currentParadigm.DeserializeArgs(config.Paradigm.Args);
            _deviceConfigPanel.DeviceConfigs = config.Devices;
        }

        public void LoadConfig(uint snapshotIdx = 0)
        {
            using (_configReadWriteLock.AcquireWriteLock())
            {
                _config = JsonUtils.DeserializeFromFile<WindowConfig>(GetConfigFilePath(snapshotIdx)) ?? new WindowConfig();

                _subjectTextBox.Text = _config.Subject ?? _subjectTextBox.Text;
                _sessionDescriptorTextBox.Text = _config.SessionDescriptor ?? _sessionDescriptorTextBox.Text;

                var oldIdx = _paradigmComboBox.SelectedIndex;
                _paradigmComboBox.FindAndSelectFirstByTag(tag => Equals((tag as ParadigmTemplate)?.Identifier, _config.SelectedParadigm), 0);
                var newIdx = _paradigmComboBox.SelectedIndex;

                if (oldIdx == newIdx) DeserializeParadigmConfig();
                DeserializeDevicesConfig();
            }
        }

        public void SaveConfig(uint snapshotIdx = 0)
        {
            using (_configReadWriteLock.AcquireWriteLock())
            {
                _config.Subject = _subjectTextBox.Text;
                _config.SessionDescriptor = _sessionDescriptorTextBox.Text;
                _config.SelectedParadigm = _currentParadigm?.Identifier;

                SerializeParadigmConfig();
                SerializeDevicesConfig();

                _config.JsonSerializeToFile(GetConfigFilePath(snapshotIdx), JsonUtils.PrettyFormat);
            }
        }

        private void UpdateFullSessionName(ParadigmTemplate paradigm, IReadonlyContext args)
        {
            if (paradigm != null)
            {
                try
                {
                    _sessionFullNameTextBlock.Foreground = Brushes.DarkGray;
                    _sessionFullNameTextBlock.Text = SessionConfigExt.GetFullSessionName(_subjectTextBox.Text, _sessionDescriptorTextBox.Text, args);
                    return;
                }
                catch (Exception e)
                {
                    Logger.Warn("UpdateFullSessionName - update full session name", e, "sessionDescriptor", _sessionDescriptorTextBox.Text);
                    _sessionFullNameTextBlock.Text = "<SYNTAX ERR>";
                }
            }
            else
                _sessionFullNameTextBlock.Text = "<UNEXPECTED ERR>";
            _sessionFullNameTextBlock.Foreground = Brushes.DarkRed;
        }

        private void OnParadigmArgsUpdated()
        {
            var paradigmTemplate = _currentParadigm;
            if (paradigmTemplate == null) return;
            if (!CheckParadigmArgs(false)) return;
            var context = ParadigmParamPanel.Context;
            UpdateFullSessionName(paradigmTemplate, context);
            Bootstrap.TryInitiateParadigm(paradigmTemplate, context, out var paradigm, false);
            ParadigmSummaryPanel.Update(context, paradigm);
            _configDirty.Set();
        }

        private void InitializeHeaderPanel()
        {
            var sessionPanel = HeaderPanel.AddGroupStackPanel("Session", "General Session Information");
            sessionPanel.AddLabeledRow("Subject", _subjectTextBox = new TextBox { Text = "Anonymous", MaxLength = 32});
            sessionPanel.AddLabeledRow("Session Descriptor", _sessionDescriptorTextBox = new TextBox {Text = "Unnamed", MaxLength = 64});
            sessionPanel.AddLabeledRow("", _sessionFullNameTextBlock = new TextBlock {FontSize = 10, Foreground = Brushes.DarkGray, Margin = new Thickness {Top = 3}});
            void OnSessionInfoChanged(object sender, TextChangedEventArgs e) => UpdateFullSessionName(_currentParadigm, ParadigmParamPanel.Context);
            _subjectTextBox.TextChanged += OnSessionInfoChanged;
            _sessionDescriptorTextBox.TextChanged += OnSessionInfoChanged;
            var paradigmPanel = HeaderPanel.AddGroupStackPanel("Paradigm", "Paradigm Selection");
            var paradigmComboBoxContainer = new Grid();
            _paradigmComboBox = new ParadigmComboBox {Margin = new Thickness {Right = ViewConstants.DefaultRowHeight + ViewConstants.MinorSpacing}};
            _paradigmComboBox.SelectionChanged += ParadigmComboBox_OnSelectionChanged;
            paradigmComboBoxContainer.Children.Add(_paradigmComboBox);
            var loadDefaultCfgBtnImageSource = new BitmapImage(new Uri(ViewConstants.ResetImageUri, UriKind.Absolute));
            var loadDefaultCfgImage = new Image {Margin = new Thickness(2), Source = loadDefaultCfgBtnImageSource};
            var loadDefaultCfgBtn = new Button {ToolTip = "Load Default Config", HorizontalAlignment = HorizontalAlignment.Right, Width = ViewConstants.DefaultRowHeight, Content = loadDefaultCfgImage};
            loadDefaultCfgBtn.Click += ParadigmResetBtn_OnClick;
            paradigmComboBoxContainer.Children.Add(loadDefaultCfgBtn);
            paradigmPanel.AddLabeledRow("Paradigm", paradigmComboBoxContainer);
            _paradigmDescriptionRow = paradigmPanel.AddLabeledRow("", _paradigmDescriptionTextBlock = new TextBlock {FontSize = 10, Foreground = Brushes.DarkGray});
            _paradigmDescriptionRow.Visibility = Visibility.Collapsed;
        }

        private void InitializeFooterPanel()
        {
            _deviceConfigPanel = new DeviceSelectionPanel();
            _deviceConfigPanel.DeviceChanged += (sender, e) =>
            {
                SerializeDeviceConfig(e.OldDevice, e.OldDeviceArgs);
                e.NewDeviceArgs = DeserializeDeviceConfig(e.NewDevice);
            };
            _deviceConfigPanel.ConsumerChanged += (sender, e) =>
            {
                SerializeConsumerConfig(e.OldConsumer, e.OldConsumerArgs);
                e.NewConsumerArgs = DeserializeConsumerConfig(e.NewConsumer);
            };
            FooterPanel.Children.Add(_deviceConfigPanel);
        }

        private void InitializeParadigmConfigurationPanel(ParadigmTemplate paradigm)
        {
            _paradigmDescriptionTextBlock.Text = paradigm.Attribute.Description;
            _paradigmDescriptionRow.Visibility = string.IsNullOrWhiteSpace(_paradigmDescriptionTextBlock.Text) 
                ? Visibility.Collapsed : Visibility.Visible;

            ParadigmParamPanel.SetDescriptors(paradigm.Factory as IParameterPresentAdapter, paradigm.Factory.GetParameterGroups(paradigm.Clz));
            ParadigmSummaryPanel.SetSummaries(paradigm.Factory as ISummaryPresentAdapter, paradigm.Factory.GetSummaries(paradigm.Clz));

            ScrollView.InvalidateScrollInfo();
            ScrollView.ScrollToTop();

            _currentParadigm = paradigm;
            // OnParadigmArgsUpdated();
            _needResizeWindow = true;
        }
        
        /// <summary>
        /// Serialize paradigm config to WindowConfig.
        /// </summary>
        private void SerializeParadigmConfig()
        {
            var paradigm = _currentParadigm;
            if (paradigm == null) return;
            using (_configReadWriteLock.AcquireWriteLock())
            {
                _config.SetParadigm(new SerializedObject(paradigm.Identifier,
                    paradigm.Attribute.Version?.ToString(),
                    paradigm.SerializeArgs(ParadigmParamPanel.Context)));
                _configDirty.Set();
            }
        }

        /// <summary>
        /// Deserialize paradigm config from WindowConfig.
        /// </summary>
        private void DeserializeParadigmConfig()
        {
            var paradigm = _currentParadigm;
            if (paradigm == null) return;
            SerializedObject serializedParadigm;
            using (_configReadWriteLock.AcquireReadLock())
                serializedParadigm = _config.GetParadigm(paradigm.Identifier);
            ParadigmParamPanel.Context = (IReadonlyContext) paradigm.DeserializeArgs(serializedParadigm.Args) ?? EmptyContext.Instance;
        }

        /// <summary>
        /// Serialize config of all devices to WindowConfig.
        /// </summary>
        private void SerializeDevicesConfig()
        {
            foreach (var deviceType in _deviceConfigPanel.DeviceTypes)
            {
                var deviceArgs = _deviceConfigPanel[deviceType];
                using (_configReadWriteLock.AcquireWriteLock())
                {
                    _config.SelectedDevices[deviceType.Name] = deviceArgs.Device.Id;
                    _config.SetDevice(deviceArgs.Device);
                    _config.SelectedConsumers[deviceType.Name] = deviceArgs.Consumers?.Select(c => c.Id).ToArray();
                    if (deviceArgs.Consumers != null)
                        foreach (var consumerEntity in deviceArgs.Consumers)
                            _config.SetConsumer(consumerEntity);
                    _configDirty.Set();
                }
            }
        }

        /// <summary>
        /// Serialize device config to WindowConfig.
        /// <param name="device">Target device</param>
        /// <param name="args">Arguments to serialize</param>
        /// </summary>
        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        private void SerializeDeviceConfig(DeviceTemplate device, IReadonlyContext args)
        {
            if (device == null) return;
            using (_configReadWriteLock.AcquireWriteLock())
            {
                _config.SetDevice(new SerializedObject(device.Identifier, device.SerializeArgs(args)));
                _configDirty.Set();
            }
        }

        /// <summary>
        /// Deserialize config of all devices from WindowConfig.
        /// </summary>
        private void DeserializeDevicesConfig()
        {
            foreach (var deviceType in _deviceConfigPanel.DeviceTypes)
                using (_configReadWriteLock.AcquireReadLock())
                    _deviceConfigPanel[deviceType] = new DeviceConfig
                    {
                        DeviceType = deviceType.Name,
                        Device = _config.SelectedDevices.TryGetValue(deviceType.Name, out var did) ? _config.GetDevice(did) : default,
                        Consumers = _config.SelectedConsumers.TryGetValue(deviceType.Name, out var consumerIds) && consumerIds != null
                            ? consumerIds.Select(cid => _config.GetConsumer(cid)).ToArray() : EmptyArray<SerializedObject>.Instance
                    };
        }

        /// <summary>
        /// Deserialize device config from WindowConfig.
        /// <param name="device">Target device</param>
        /// </summary>
        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        private IReadonlyContext DeserializeDeviceConfig(DeviceTemplate device)
        {
            if (device == null) return EmptyContext.Instance;
            using (_configReadWriteLock.AcquireReadLock())
                return (IReadonlyContext)device.DeserializeArgs(_config.GetDevice(device.Identifier).Args) ?? EmptyContext.Instance; 
        }

        /// <summary>
        /// Serialize consumer config to WindowConfig.
        /// <param name="consumer">Target consumer</param>
        /// <param name="args">Arguments to serialize</param>
        /// </summary>
        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        private void SerializeConsumerConfig(ConsumerTemplate consumer, IReadonlyContext args)
        {
            if (consumer == null) return;
            using (_configReadWriteLock.AcquireWriteLock())
            {
                _config.SetConsumer(new SerializedObject(consumer.Identifier, consumer.SerializeArgs(args)));
                _configDirty.Set();
            }
        }

        /// <summary>
        /// Deserialize consumer config from WindowConfig.
        /// <param name="consumer">Target consumer</param>
        /// </summary>
        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        private IReadonlyContext DeserializeConsumerConfig(ConsumerTemplate consumer)
        {
            if (consumer == null) return EmptyContext.Instance;
            using (_configReadWriteLock.AcquireReadLock()) 
                return (IReadonlyContext)consumer.DeserializeArgs(_config.GetConsumer(consumer.Identifier).Args) ?? EmptyContext.Instance; 
        }

        /// <summary>
        /// Check the arguments for current paradigm.
        /// <param name="msgBox">Show message box of invalid parameters</param>
        /// <returns>true if all the arguments are valid</returns>
        /// </summary>
        private bool CheckParadigmArgs(bool msgBox = true)
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
                        try { valid = factory.CheckValid(paradigm.Clz, context, pd); }
                        catch (Exception e) { Logger.Warn("CheckParadigmArgs", e, "parameter", pd.Key); }
                        var row = ParadigmParamPanel[pd]?.Row;
                        if (row != null && (row.IsError = valid.IsFailed)) row.ErrorMessage = valid.Message?.Trim();
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

        /// <summary>
        /// Refresh combobox for supported paradigms.
        /// </summary>
        private void RefreshParadigmComboboxItems()
        {
            var categoryParadigms = new SortedDictionary<string, LinkedList<ParadigmTemplate>>();
            foreach (var paradigm in App.Instance.Registries.Registry<ParadigmTemplate>().Registered)
            {
                var category = paradigm.Category ?? "Default";
                (categoryParadigms.TryGetValue(category, out var list) 
                    ? list : categoryParadigms[category] = list = new LinkedList<ParadigmTemplate>()).AddLast(paradigm);
            }
            var paradigmItems = new LinkedList<object>();
            // ReSharper disable once ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
            foreach (var paradigms in categoryParadigms.Values)
            foreach (var paradigm in paradigms)
            {
                var stackPanel = new StackPanel {Orientation = Orientation.Horizontal, Tag = paradigm};
                stackPanel.Children.Add(new TextBlock {Text = paradigm.Identifier, VerticalAlignment = VerticalAlignment.Center});
                if (paradigm.Version != null)
                {
                    stackPanel.Children.Add(new TextBlock
                    {
                        Text = $"v{paradigm.Version}",
                        Margin = new Thickness(5, 0, 0, 0),
                        VerticalAlignment = VerticalAlignment.Center,
                        FontSize = 9,
                        Foreground = Brushes.DarkGray
                    });
                }
                paradigmItems.AddLast(stackPanel);
            }
            var listCollectionView = new ListCollectionView(paradigmItems.ToArray());
            listCollectionView.GroupDescriptions.Add(new ParadigmItemGroupDescription());
            _paradigmComboBox.ItemsSource = listCollectionView;
        }

        private void RefreshSnapshotsMenuItems()
        {
            var style = FindResource("MenuItem") as Style;
            if (!(SaveSnapshotMenuItem.ItemsSource is ObservableCollection<MenuItem> saveMenuItems))
                SaveSnapshotMenuItem.ItemsSource = saveMenuItems = new ObservableCollection<MenuItem>();
            if (!(LoadSnapshotMenuItem.ItemsSource is ObservableCollection<MenuItem> loadMenuItems))
                LoadSnapshotMenuItem.ItemsSource = loadMenuItems = new ObservableCollection<MenuItem>();
            while (saveMenuItems.Count < MaxSnapshotCount)
            {
                var snapshotMenuItem = new MenuItem {Style = style, Tag = (uint) saveMenuItems.Count};
                snapshotMenuItem.Click += SaveSnapshotMenuItem_OnClick;
                saveMenuItems.Add(snapshotMenuItem);
            } 
            while (loadMenuItems.Count < MaxSnapshotCount)
            {
                var snapshotMenuItem = new MenuItem {Style = style, Tag = (uint) loadMenuItems.Count};
                snapshotMenuItem.Click += LoadSnapshotMenuItem_OnClick;
                loadMenuItems.Add(snapshotMenuItem);
            }
            for (var i = 0; i < MaxSnapshotCount; i++)
            {
                var path = GetConfigFilePath((uint) i);
                saveMenuItems[i].Header = loadMenuItems[i].Header = !File.Exists(path) ? $"{i + 1}. (Blank)" : $"{i + 1}. {File.GetLastWriteTime(path)}";
            }
        }

        private void RefreshRecentSessionsMenuItems()
        {
            var style = FindResource("MenuItem") as Style;
            var menuItems = new LinkedList<MenuItem>();
            using (_configReadWriteLock.AcquireReadLock())
                if (_config.RecentSessions?.IsEmpty() ?? true)
                    menuItems.AddLast(new MenuItem {Style = style, Header = "None", IsEnabled = false});
                else
                    foreach (var recentSession in _config.RecentSessions)
                    {
                        var menuItem = new MenuItem {Style = style, Header = $"{menuItems.Count + 1}. {recentSession}{SessionConfig.FileSuffix}"};
                        menuItem.Click += (sender, e) => SetSessionConfig(JsonUtils.DeserializeFromFile<SessionConfig>(recentSession + SessionConfig.FileSuffix));
                        menuItems.AddLast(menuItem);
                    }
            RecentSessionsMenuItem.ItemsSource = menuItems.ToArray();
        }

        /// <summary>
        /// Refresh menu for capabilities of current platform.
        /// </summary>
        public void RefreshPlatformCapabilityMenuItems()
        {
            var style = FindResource("MenuItem") as Style;
            var desktop = GraphicsUtils.DesktopHdc;
            var capMenuItems = new LinkedList<MenuItem>();
            foreach (var deviceCap in typeof(Gdi32.DeviceCap).GetEnumValues())
            {
                var header = $"{typeof(Gdi32.DeviceCap).GetEnumName(deviceCap)}: {Gdi32.GetDeviceCaps(desktop, (int)deviceCap)}";
                capMenuItems.AddLast(new MenuItem { Style = style, Header = header });
            }
            PlatformCapsMenuItem.ItemsSource = capMenuItems.ToArray();
        }

        private void RefreshAppMenuItems()
        {
            var style = FindResource("MenuItem") as Style;
            var menuItems = new LinkedList<object>();
            var appEntries = App.Instance.Registries.Registry<AppEntryAddOn>().Registered;
            if (appEntries?.IsEmpty() ?? true)
                menuItems.AddLast(new MenuItem {Style = style, Header = "None", IsEnabled = false});
            else
                foreach (var appEntry in appEntries)
                {
                    var appEntryMenuItem = new MenuItem {Style = style, Header = $"{appEntry.Identifier} - ({appEntry.Plugin?.Name ?? "Embedded"})"};
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
                            children.AddLast(new MenuItem {Style = style, Header = $"No {deviceType.DisplayName.ToLowerInvariant()} Implementations", IsEnabled = false});
                        else
                            foreach (var device in devices)
                            {
                                var deviceAttribute = device.Attribute;
                                var menuItemHeader = $"{deviceAttribute.Name} ({deviceAttribute.FullVersionName}) - {device.Clz.FullName}";
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
                            var paradigmAttribute = paradigm.Attribute;
                            var menuItemHeader = $"{paradigmAttribute.Name} ({paradigmAttribute.FullVersionName}) - {paradigm.Clz.FullName}";
                            var paradigmMenuItem = new MenuItem { Style = style, Header = menuItemHeader };
                            paradigmMenuItem.Click += (sender, e) => _paradigmComboBox.FindAndSelectFirstByTag(paradigm, null);
                            children.AddLast(paradigmMenuItem);
                        }
                    children.AddLast(new Separator());

                    if (!plugin.Consumers.Any())
                        children.AddLast(new MenuItem {Style = style, Header = "No Stream-Consumer Implementations", IsEnabled = false});
                    else
                        foreach (var consumer in plugin.Consumers)
                        {
                            var consumerAttribute = consumer.Attribute;
                            var menuItemHeader = $"{consumerAttribute.Name} ({consumerAttribute.FullVersionName}) - {consumer.Clz.FullName}";
                            var consumerMenuItem = new MenuItem { Style = style, Header = menuItemHeader };
                            //consumerMenuItem.Click += (sender, e) => _paradigmComboBox.FindAndSelectFirstByTag(consumerAttribute.Name, null);
                            children.AddLast(consumerMenuItem);
                        }
                    children.AddLast(new Separator());

                    if (!plugin.Paradigms.Any() && !plugin.CustomMarkers.Any())
                        children.AddLast(new MenuItem {Style = style, Header = "No Custom Marker Definitions", IsEnabled = false});
                    else
                        foreach (var keyValuePair in plugin.CustomMarkers.OrderBy(pair => pair.Key))
                            children.AddLast(new MenuItem {Style = style, Header = $"{keyValuePair.Key} - {keyValuePair.Value}"});
                    menuItem.ItemsSource = children;
                    menuItems.AddLast(menuItem);
                }
            PluginsMenuItem.ItemsSource = menuItems.ToArray();
        }

        // ReSharper disable once UnusedMember.Local
        private void DisplayPopup(string title, FrameworkElement element)
        {
            PopupTitleTextBlock.Text = title;
            PopupHeaderGrid.Visibility = title == null ? Visibility.Collapsed : Visibility.Visible;
            PopupContentControl.Content = element;
            PopupGrid.Visibility = Visibility.Visible;
        }

        private void AutoSaveTimer_OnTick(object state)
        {
            lock (_configAutoSaveTimer)
            {
                if (_configDirty.Value)
                    try
                    {
                        this.DispatcherInvoke(() => SaveConfig(0));
                    }
                    catch (Exception)
                    {
                        // ignored
                    }
                _configDirty.Reset();
            }
        }

        private void Window_OnLoaded(object sender, RoutedEventArgs e)
        {
            RefreshPlatformCapabilityMenuItems();

            RefreshParadigmComboboxItems();
            _deviceConfigPanel.UpdateDevices();

            LoadConfig();

            RefreshSnapshotsMenuItems();
            RefreshRecentSessionsMenuItems();
            RefreshAppMenuItems();
            RefreshPluginMenuItems();
        }

        private void Window_OnLayoutUpdated(object sender, EventArgs e)
        {
            if (!IsVisible || !_needResizeWindow || !IsLoaded) return;
            var contentHeight = MainPanel.Children.OfType<FrameworkElement>().Sum(el => el.ActualHeight);
            var minWidth = (_currentParadigm?.Factory as IPresentAdapter)?.DesiredWidth ?? double.NaN;
            this.UpdateWindowSize(contentHeight + 50 + (ActualHeight - ScrollView.ActualHeight), minWidth);
            _needResizeWindow = false;
        }

        private void Window_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyStates == Keyboard.GetKeyStates(Key.Return) && Keyboard.Modifiers == ModifierKeys.Alt) StartSession();
        }

        private void Window_OnClosing(object sender, EventArgs e)
        {
            _configAutoSaveTimer.Dispose();
            try
            {
                SaveConfig(0);
            }
            catch (Exception)
            {
                // ignored
            }
        }

        private void Window_OnClosed(object sender, EventArgs e) => App.Kill();

        private void ParadigmComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SerializeParadigmConfig();
            InitializeParadigmConfigurationPanel((ParadigmTemplate) ((FrameworkElement) _paradigmComboBox.SelectedItem)?.Tag);
            DeserializeParadigmConfig();
        }

        private void ParadigmResetBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var paradigm = _currentParadigm;
            if (paradigm == null) return;
            ParadigmParamPanel.ResetToDefault();
            OnParadigmArgsUpdated();
        }

        private void MenuItem_OnSubmenuOpened(object sender, RoutedEventArgs e) => RefreshSnapshotsMenuItems();

        private void OpenFromMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Config File",
                Multiselect = false,
                CheckFileExists = true,
                DefaultExt = SessionConfig.FileSuffix,
                Filter = FileUtils.GetFileFilter("Session Config File", SessionConfig.FileSuffix),
                InitialDirectory = Path.GetFullPath(ConfigDir)
            };
            if (!dialog.ShowDialog(this).Value) return;
            SetSessionConfig(JsonUtils.DeserializeFromFile<SessionConfig>(dialog.FileName));
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

        private void NewMultiSessionConfigMenuItem_OnClick(object sender, RoutedEventArgs e) => new MultiSessionLauncherWindow(null).ShowDialog();

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
            new MultiSessionLauncherWindow(dialog.FileName).ShowDialog();
        }

        private void ExitMenuItem_OnClick(object sender, RoutedEventArgs e) => Close();

        private void SystemVariablesMenuItem_OnClick(object sender, RoutedEventArgs e) => App.ConfigSystemVariables();

        private void ConfigFolderMenuItem_OnClick(object sender, RoutedEventArgs e) => Process.Start(Path.GetFullPath(ConfigDir));

        private void DataFolderMenuItem_OnClick(object sender, RoutedEventArgs e) => Process.Start(Path.GetFullPath(App.DataDir));

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

        private void SaveSnapshotMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (!CheckParadigmArgs()) return;
            SaveConfig((uint)((MenuItem)sender).Tag);
        }

        private void LoadSnapshotMenuItem_OnClick(object sender, RoutedEventArgs e) => LoadConfig((uint)((MenuItem)sender).Tag);

        private void ParadigmParamPanel_OnLayoutChanged(object sender, LayoutChangedEventArgs e) => _needResizeWindow = true;

        private void ParadigmParamPanel_OnContextChanged(object sender, ContextChangedEventArgs e)
        {
            if (!CheckParadigmArgs(false)) return;
            OnParadigmArgsUpdated();
        }

        private void StartBtn_OnClick(object sender, RoutedEventArgs e) => StartSession();

        public void BeforeAllSessionsStart() { }

        void Bootstrap.ISessionListener.BeforeSessionStart(int index, Session session)
        {
            using (_configReadWriteLock.AcquireWriteLock())
            {
                _config.AddRecentSession(session.DataFilePrefix);
                _configDirty.Set();
            }
            RefreshRecentSessionsMenuItems();
            SaveConfig();
        }

        void Bootstrap.ISessionListener.AfterSessionCompleted(int index, Session session) { }

        void Bootstrap.ISessionListener.AfterAllSessionsCompleted(Session[] sessions)
        {
            foreach (var session in sessions)
                if (session?.Result != null)
                    new ResultWindow(session).Show();
        }

    }

}
