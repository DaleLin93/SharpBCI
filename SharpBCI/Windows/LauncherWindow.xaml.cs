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
using System.Windows.Media.Imaging;
using JetBrains.Annotations;
using MarukoLib.Interop;
using MarukoLib.IO;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Presenters;
using SharpBCI.Registrables;
using SharpBCI.Extensions.Windows;
using MarukoLib.Logging;
using MarukoLib.Persistence;
using MarukoLib.UI;
using SharpBCI.Extensions.Experiments;
using ValidationResult = SharpBCI.Extensions.Experiments.ValidationResult;

namespace SharpBCI.Windows
{

    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    partial class LauncherWindow
    {

        [SuppressMessage("ReSharper", "FieldCanBeMadeReadOnly.Local")]
        internal class WindowConfig
        {

            public const string CurrentVersion = "0.1";

            public const int MaxRecentExperimentCount = 10;

            public string Version = CurrentVersion;

            public string Subject;

            public string SessionName;

            public string SelectedExperiment;

            public IDictionary<string, string> SelectedDevices = new Dictionary<string, string>();

            public IDictionary<string, string[]> SelectedConsumers = new Dictionary<string, string[]>();

            public LinkedList<string> RecentExperiments = new LinkedList<string>();

            public IList<ParameterizedEntity> Experiments = new List<ParameterizedEntity>();

            public IList<ParameterizedEntity> DeviceParams = new List<ParameterizedEntity>();

            public IList<ParameterizedEntity> ConsumerParams = new List<ParameterizedEntity>();

            public ParameterizedEntity GetExperiment(string name) => Experiments.FirstOrDefault(entity => Equals(entity.Id, name));

            public ParameterizedEntity GetDevice( string name) => DeviceParams.FirstOrDefault(entity => Equals(entity.Id, name));

            public ParameterizedEntity GetConsumer(string name) => ConsumerParams.FirstOrDefault(entity => Equals(entity.Id, name));

            public void SetExperiment(ParameterizedEntity entity) => Set(Experiments, entity);

            public void SetDevice(ParameterizedEntity entity) => Set(DeviceParams, entity);

            public void SetConsumer(ParameterizedEntity entity) => Set(ConsumerParams, entity);

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

        private Grid _experimentDescriptionRow;

        private TextBlock _sessionFullNameTextBlock, _experimentDescriptionTextBlock;

        private ComboBox _experimentComboBox;

        private DeviceSelectionPanel _deviceConfigPanel;

        private bool _needResizeWindow;

        /* Config */

        private WindowConfig _config = new WindowConfig();

        /* Temporary variables */

        private RegistrableExperiment _currentExperiment;

        static LauncherWindow() => Directory.CreateDirectory(ConfigDir);

        public LauncherWindow()
        {
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

        public void StartExperiment()
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
            Session session = null;
            try
            {
                lock (StartBtn)
                {
                    if (!ValidateExperimentParams()) return;
                    if (!StartBtn.IsEnabled) return;
                    StartBtn.IsEnabled = false;
                    Visibility = Visibility.Hidden;
                }
                App.StartExperiment(GetSessionConfig(), MonitorWindow.IsShown, current =>
                {
                    AddRecentExperimentItems(current.DataFilePrefix);
                    SaveConfig();
                }, currentSession => session = currentSession);
            }
            catch (Exception ex)
            {
                Logger.Error("StartExperiment", ex);
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
            if (session?.Result != null) new ResultWindow(session).Show();
        }

        public SessionConfig.Experiment GetSessionExperimentPart(RegistrableExperiment experiment, IReadonlyContext @params) =>
            new SessionConfig.Experiment
            {
                Subject = _subjectTextBox.Text,
                SessionDescriptor = _sessionDescriptorTextBox.Text,
                Params = new ParameterizedEntity(experiment.Identifier, 
                    experiment.Attribute.Version?.ToString(), experiment.SerializeParams(@params))
            };

        public SessionConfig GetSessionConfig()
        {
            var experiment = (RegistrableExperiment)_experimentComboBox.SelectedItem ?? throw new UserException("No experiment was selected");
            return new SessionConfig
            {
                ExperimentPart = GetSessionExperimentPart(experiment, ExperimentParamPanel.Context),
                DevicePart = _deviceConfigPanel.DeviceConfig,
                Monitor = MonitorWindow.IsShown
            };
        }

        public void SetSessionConfig(SessionConfig config)
        {
            SetSessionGeneralPart(config.ExperimentPart);
            _deviceConfigPanel.DeviceConfig = config.DevicePart;
        }

        public void SetSessionGeneralPart(SessionConfig.Experiment part)
        {
            _subjectTextBox.Text = part.Subject ?? _subjectTextBox.Text;
            _sessionDescriptorTextBox.Text = part.SessionDescriptor ?? _sessionDescriptorTextBox.Text;
            if (_experimentComboBox.FindAndSelect(part.Params.Id, null))
                ExperimentParamPanel.Context = _currentExperiment.DeserializeParams(part.Params.Params);
        }

        public void LoadConfig()
        {
            _config = JsonUtils.DeserializeFromFile<WindowConfig>(ConfigFile) ?? new WindowConfig();

            _subjectTextBox.Text = _config.Subject ?? _subjectTextBox.Text;
            _sessionDescriptorTextBox.Text = _config.SessionName ?? _sessionDescriptorTextBox.Text;
            _experimentComboBox.FindAndSelect(_config.SelectedExperiment, 0);

            DeserializeExperimentConfig();
            DeserializeDevicesConfig();
        }

        public void SaveConfig()
        {
            _config.Subject = _subjectTextBox.Text;
            _config.SessionName = _sessionDescriptorTextBox.Text;
            _config.SelectedExperiment = (_experimentComboBox.SelectedItem as RegistrableExperiment)?.Identifier;

            SerializeExperimentConfig();
            SerializeDevicesConfig();

            _config.JsonSerializeToFile(ConfigFile, JsonUtils.PrettyFormat);
        }

        private void UpdateFullSessionName(RegistrableExperiment experiment, IReadonlyContext @params)
        {
            if (experiment != null)
            {
                try
                {
                    _sessionFullNameTextBlock.Foreground = Brushes.DarkGray;
                    _sessionFullNameTextBlock.Text = GetSessionExperimentPart(experiment, @params).GetFullSessionName();
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

        private void OnExperimentParamsUpdated()
        {
            var registrableExperiment = _currentExperiment;
            if (registrableExperiment == null) return;
            if (!ValidateExperimentParams(false)) return;

            var context = ExperimentParamPanel.Context;
            UpdateFullSessionName(registrableExperiment, context);
            App.TryInitiateExperiment(registrableExperiment, context, out var experiment, false);
            ExperimentSummaryPanel.Update(context, experiment);
        }

        private void InitializeHeaderPanel()
        {
            var sessionPanel = HeaderPanel.AddGroupPanel("Session", "General Session Information");
            sessionPanel.AddRow("Subject", _subjectTextBox = new TextBox { Text = "Anonymous", MaxLength = 32});
            sessionPanel.AddRow("Session Descriptor", _sessionDescriptorTextBox = new TextBox { Text = "Unnamed", MaxLength = 64 });
            sessionPanel.AddRow("", _sessionFullNameTextBlock = new TextBlock {FontSize = 10, Foreground = Brushes.DarkGray, Margin = new Thickness {Top = 3}});
            void OnSessionInfoChanged(object sender, TextChangedEventArgs e) => UpdateFullSessionName(_currentExperiment, ExperimentParamPanel.Context);
            _subjectTextBox.TextChanged += OnSessionInfoChanged;
            _sessionDescriptorTextBox.TextChanged += OnSessionInfoChanged;
            var experimentPanel = HeaderPanel.AddGroupPanel("Exp.", "Experiment Selection");
            var experimentComboBoxContainer = new Grid();
            _experimentComboBox = new ComboBox {Margin = new Thickness {Right = ViewConstants.DefaultRowHeight + ViewConstants.MinorSpacing}};
            _experimentComboBox.SelectionChanged += ExperimentComboBox_OnSelectionChanged;
            experimentComboBoxContainer.Children.Add(_experimentComboBox);
            var loadDefaultCfgBtnImageSource = new BitmapImage(new Uri("pack://application:,,,/Resources/reset.png", UriKind.Absolute));
            var loadDefaultCfgImage = new Image {Margin = new Thickness(2), Source = loadDefaultCfgBtnImageSource};
            var loadDefaultCfgBtn = new Button {ToolTip = "Load Default Config", HorizontalAlignment = HorizontalAlignment.Right, Width = ViewConstants.DefaultRowHeight, Content = loadDefaultCfgImage};
            loadDefaultCfgBtn.Click += ExperimentResetBtn_OnClick;
            experimentComboBoxContainer.Children.Add(loadDefaultCfgBtn);
            experimentPanel.AddRow("Experiment", experimentComboBoxContainer, ViewConstants.DefaultRowHeight);
            _experimentDescriptionRow = experimentPanel.AddRow("", _experimentDescriptionTextBlock = new TextBlock { FontSize = 10, Foreground = Brushes.DarkGray });
            _experimentDescriptionRow.Visibility = Visibility.Collapsed;
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

        private void InitializeExperimentConfigurationPanel(RegistrableExperiment experiment)
        {
            _experimentDescriptionTextBlock.Text = experiment.Attribute.Description;
            _experimentDescriptionRow.Visibility = string.IsNullOrWhiteSpace(_experimentDescriptionTextBlock.Text) 
                ? Visibility.Collapsed : Visibility.Visible;

            ExperimentParamPanel.Descriptors = experiment.Factory.ParameterGroups.Cast<IDescriptor>().ToArray();
            ExperimentParamPanel.Adapter = experiment.Factory as IParameterPresentAdapter;

            ExperimentSummaryPanel.Summaries = experiment.Factory.Summaries.ToArray();
            ExperimentSummaryPanel.Adapter = experiment.Factory as ISummaryPresentAdapter;

            ScrollView.InvalidateScrollInfo();
            ScrollView.ScrollToTop();

            _currentExperiment = experiment;
            OnExperimentParamsUpdated();
            _needResizeWindow = true;
        }
        
        private void SerializeExperimentConfig()
        {
            var experiment = _currentExperiment;
            if (experiment == null) return;
            _config.SetExperiment(new ParameterizedEntity(experiment.Identifier, 
                experiment.Attribute.Version?.ToString(), 
                experiment.SerializeParams(ExperimentParamPanel.Context)));
        }

        private void DeserializeExperimentConfig()
        {
            var experiment = _currentExperiment;
            if (experiment == null) return;
            var entity = _config.GetExperiment(experiment.Identifier);
            ExperimentParamPanel.Context = (IReadonlyContext) experiment.DeserializeParams(entity.Params) ?? EmptyContext.Instance;
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
        private void SerializeDeviceConfig(RegistrableDevice device, IReadonlyContext @params)
        {
            if (device == null) return;
            _config.SetDevice(new ParameterizedEntity(device.Identifier, device.SerializeParams(@params)));
        }

        private void DeserializeDevicesConfig()
        {
            foreach (var deviceType in _deviceConfigPanel.DeviceTypes)
                _deviceConfigPanel[deviceType] = new DeviceParams
                {
                    Device = _config.SelectedDevices.TryGetValue(deviceType.Name, out var did) ? _config.GetDevice(did) : default,
                    Consumers = _config.SelectedConsumers.TryGetValue(deviceType.Name, out var consumerIds) && consumerIds != null 
                        ? consumerIds.Select(cid => _config.GetConsumer(cid)).ToArray() : EmptyArray<ParameterizedEntity>.Instance
                };
        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        private IReadonlyContext DeserializeDeviceConfig(RegistrableDevice device) =>
            device == null ? EmptyContext.Instance : (IReadonlyContext)device.DeserializeParams(_config.GetDevice(device.Identifier).Params) ?? EmptyContext.Instance;

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        private void SerializeConsumerConfig(RegistrableConsumer consumer, IReadonlyContext @params)
        {
            if (consumer == null) return;
            _config.SetConsumer(new ParameterizedEntity(consumer.Identifier, consumer.SerializeParams(@params)));
        }

        [SuppressMessage("ReSharper", "SuggestBaseTypeForParameter")]
        private IReadonlyContext DeserializeConsumerConfig(RegistrableConsumer consumer) =>
            consumer == null ? EmptyContext.Instance : (IReadonlyContext)consumer.DeserializeParams(_config.GetConsumer(consumer.Identifier).Params) ?? EmptyContext.Instance;

        private bool ValidateExperimentParams(bool msgBox = true)
        {
            var factory = _currentExperiment?.Factory;
            if (factory == null) return false;
            var adapter = factory as IParameterPresentAdapter;
            var invalidParamValidationResults = ExperimentParamPanel.GetInvalidParams()
                .Select(p => new ParamValidationResult(p, ValidationResult.Failed(null)))
                .ToList();
            if (invalidParamValidationResults.Count <= 0)
            {
                var context = ExperimentParamPanel.Context;
                invalidParamValidationResults = context.Properties
                    .Where(cp => cp is IParameterDescriptor pd && (adapter?.IsVisible(context, pd) ?? true))
                    .Select(cp => (IParameterDescriptor)cp)
                    .Select(pd =>
                    {
                        var valid = ValidationResult.Failed();
                        try { valid = factory.IsValid(context, pd); }
                        catch (Exception e) { Logger.Warn("ValidateExperimentParams", e, "parameter", pd.Key); }
                        return new ParamValidationResult(pd, valid);
                    })
                    .Where(result => result.Result.IsFailed)
                    .ToList();
                if (invalidParamValidationResults.Count <= 0)
                {
                    SetErrorMessage(null);
                    return true;
                }
                foreach (var paramValidationResult in invalidParamValidationResults)
                    ExperimentParamPanel.SetParamState(paramValidationResult.Param, ParameterStateType.Valid, false);
            }
            var stringBuilder = new StringBuilder("The following parameters of experiment are invalid");
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

        private void AddRecentExperimentItems(string prefix)
        {
            if (_config.RecentExperiments == null)
                _config.RecentExperiments = new LinkedList<string>();
            _config.RecentExperiments.AddFirst(prefix);
            if (_config.RecentExperiments.Count > WindowConfig.MaxRecentExperimentCount)
                _config.RecentExperiments.RemoveLast();
            RefreshRecentExperimentMenuItems();
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

        private void RefreshRecentExperimentMenuItems()
        {
            var style = (Style) FindResource("MenuItem");
            var menuItems = new LinkedList<MenuItem>();
            if (_config.RecentExperiments?.IsEmpty() ?? true)
                menuItems.AddLast(new MenuItem {Style = style, Header = "None", IsEnabled = false});
            else
                foreach (var experiment in _config.RecentExperiments)
                {
                    var menuItem = new MenuItem {Style = style, Header = experiment};
                    menuItem.Click += (sender, e) => SetSessionConfig(JsonUtils.DeserializeFromFile<SessionConfig>(experiment + SessionConfig.FileSuffix));
                    menuItems.AddLast(menuItem);
                }
            LoadFromRecentExperimentsMenuItem.ItemsSource = menuItems.ToArray();
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

                    if (!plugin.AppEntries.Any())
                        children.AddLast(new MenuItem { Style = style, Header = "No App Entry Implementations", IsEnabled = false });
                    else
                        foreach (var appEntry in plugin.AppEntries)
                        {
                            var appEntryMenuItem = new MenuItem { Style = style, Header = $"{appEntry.Name} - {appEntry.GetType().FullName}" };
                            appEntryMenuItem.Click += (sender, e) => appEntry.Run();
                            children.AddLast(appEntryMenuItem);
                        }
                    children.AddLast(new Separator());

                    if (!plugin.ExperimentFactories.Any())
                        children.AddLast(new MenuItem { Style = style, Header = "No Experiment Implementations", IsEnabled = false });
                    else
                        foreach (var experimentFactory in plugin.ExperimentFactories)
                        {
                            var experimentAttribute = experimentFactory.ExperimentType.GetExperimentAttribute();
                            var menuItemHeader = $"{experimentAttribute.Name} ({experimentAttribute.FullVersionName}) - {experimentFactory.GetType().FullName}";
                            var experimentMenuItem = new MenuItem {Style = style, Header = menuItemHeader};
                            experimentMenuItem.Click += (sender, e) => _experimentComboBox.FindAndSelect(experimentAttribute.Name, null);
                            children.AddLast(experimentMenuItem);
                        }
                    children.AddLast(new Separator());

                    foreach (var deviceType in _deviceConfigPanel.DeviceTypes)
                    {
                        var deviceFactories = plugin.DeviceFactories[deviceType];
                        if (deviceFactories.Count <= 0)
                            children.AddLast(new MenuItem { Style = style, Header = $"No {deviceType.DisplayName.ToLowerInvariant()} Implementations", IsEnabled = false });
                        else
                            foreach (var factory in deviceFactories)
                            {
                                var deviceMenuItem = new MenuItem { Style = style, Header = $"{factory.DeviceName} - {factory.GetType().FullName}" };
                                deviceMenuItem.Click += (sender, e) => _deviceConfigPanel.FindAndSelectDevice(deviceType, factory.DeviceName, null);
                                children.AddLast(deviceMenuItem);
                            }
                        children.AddLast(new Separator());
                    }

                    if (!plugin.ExperimentFactories.Any() && !plugin.CustomMarkers.Any())
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

            _experimentComboBox.ItemsSource = App.Instance.Registries.Registry<RegistrableExperiment>().Registered.OrderBy(exp => exp.Identifier);
            _deviceConfigPanel.UpdateDevices();

            LoadConfig();

            RefreshRecentExperimentMenuItems();
            RefreshPluginMenuItems();
        }

        private void Window_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyStates == Keyboard.GetKeyStates(Key.Return) && Keyboard.Modifiers == ModifierKeys.Alt) StartExperiment();
        }

        private void Window_OnClosed(object sender, EventArgs e) => App.Kill();

        private void Window_OnLayoutUpdated(object sender, EventArgs e)
        {
            if (!_needResizeWindow) return;
            var point = PointToScreen(new Point(ActualWidth / 2, ActualHeight / 2));
            var screen = System.Windows.Forms.Screen.FromPoint(point.RoundToSdPoint());
            var scaleFactor = GraphicsUtils.Scale;
            var maxHeight = screen.WorkingArea.Height / scaleFactor;
            var contentHeight = MainPanel.Children.OfType<FrameworkElement>().Sum(el => el.ActualHeight);
            Height = Math.Min(contentHeight + 15 + (ActualHeight - ScrollView.ActualHeight), maxHeight);
            var offset = screen.WorkingArea.Bottom / scaleFactor - (Top + ActualHeight);
            if (offset < 0) Top += offset;
            _needResizeWindow = false;
        }

        private void ExperimentComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SerializeExperimentConfig();
            var registrableExperiment = (RegistrableExperiment) _experimentComboBox.SelectedItem;
            InitializeExperimentConfigurationPanel(registrableExperiment);
            DeserializeExperimentConfig();
        }

        private void ExperimentResetBtn_OnClick(object sender, RoutedEventArgs e)
        {
            var registrableExperiment = _currentExperiment;
            if (registrableExperiment == null) return;
            ExperimentParamPanel.ResetToDefault();
            OnExperimentParamsUpdated();
        }

        private void SaveMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (!ValidateExperimentParams()) return;
            SaveConfig();
        }

        private void ReloadMenuItem_OnClick(object sender, RoutedEventArgs e) => LoadConfig();

        private void NewAutoRunConfigMenuItem_OnClick(object sender, RoutedEventArgs e) => new AutoRunConfigWindow(null).ShowDialog();

        private void OpenAutoRunConfigMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Config File",
                Multiselect = false,
                CheckFileExists = true,
                DefaultExt = AutoRunConfig.FileSuffix,
                Filter = FileUtils.GetFileFilter("Auto-Run Config File", AutoRunConfig.FileSuffix),
                InitialDirectory = Path.GetFullPath(ConfigDir)
            };
            if (!dialog.ShowDialog(this).Value) return;
            new AutoRunConfigWindow(dialog.FileName).ShowDialog();
        }

        private void SaveAsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var sessionConfig = GetSessionConfig();
            var defaultFileName = $"{sessionConfig.ExperimentPart.Subject}-{sessionConfig.ExperimentPart.GetFormattedSessionDescriptor()}{SessionConfig.FileSuffix}"
                .RemoveInvalidCharacterForFileName();
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

        private void ExperimentParamPanel_OnLayoutChanged(object sender, LayoutChangedEventArgs e) => _needResizeWindow = true;

        private void ExperimentParamPanel_OnContextChanged(object sender, ContextChangedEventArgs e)
        {
            if (!ValidateExperimentParams(false)) return;
            OnExperimentParamsUpdated();
        }

        private void StartBtn_OnClick(object sender, RoutedEventArgs e) => StartExperiment();

    }

}
