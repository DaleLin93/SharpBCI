using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.Logging;
using MarukoLib.Persistence;
using Microsoft.Win32;
using SharpBCI.Core.Experiment;

namespace SharpBCI.Windows
{

    /// <inheritdoc cref="Window" />
    /// <summary>
    /// Interaction logic for MultiSessionLauncherWindow.xaml
    /// </summary>
    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    [SuppressMessage("ReSharper", "UnusedMember.Global")]
    partial class MultiSessionLauncherWindow : Bootstrap.ISessionListener
    {

        public const string DeviceFileSuffix = ".dcfg";

        public static Logger Logger = Logger.GetLogger(typeof(MultiSessionLauncherWindow));

        public class FileItem<T>
        {

            public readonly string FilePath;

            public readonly T Value;

            public FileItem(string filePath, T value)
            {
                FilePath = filePath;
                Value = value;
            }

            public string FileName => Path.GetFileName(FilePath);

        }

        public class ParadigmItem : FileItem<SerializedObject>
        {

            public ParadigmItem(string filePath, SerializedObject value) : base(filePath, value) { }

            public string ParadigmId => Value.Id;

        }

        public class DeviceItem : FileItem<DeviceConfig[]>
        {

            public DeviceItem(string filePath, DeviceConfig[] value) : base(filePath, value) { }

        }

        public class SessionListViewItem
        {

            public SessionListViewItem(string sessionDescriptor, ParadigmItem paradigm)
            {
                SessionDescriptor = sessionDescriptor;
                Paradigm = paradigm;
            }

            public string SessionDescriptor { get; }

            public ParadigmItem Paradigm { get; }

        }

        private readonly ObservableCollection<SessionListViewItem> _sessionListViewItems = new ObservableCollection<SessionListViewItem>();
        
        private string _msCfgFile;

        public MultiSessionLauncherWindow([CanBeNull] string msCfgFile = null)
        {
            InitializeComponent();

            SessionListView.ItemsSource = _sessionListViewItems;
            _sessionListViewItems.CollectionChanged += SessionListViewItemsCollectionOnChanged;

            _msCfgFile = msCfgFile;
        }

        public bool IsKillOnFinish { get; set; } = false;

        private static SessionListViewItem ReadSessionConfig(MultiSessionConfig.SessionItem sessionItem, string alternativeDirectory) => 
            new SessionListViewItem(sessionItem.SessionDescriptor, ReadSessionConfig(sessionItem.ParadigmConfigPath, alternativeDirectory).Paradigm);

        private static SessionListViewItem ReadSessionConfig(string path, string alternativeDirectory)
        {
            if (!App.FindFile(alternativeDirectory, path, out var paradigmFilePath))
                throw new UserException($"Paradigm config file not found: {path}");
            if (path.EndsWith(SessionConfig.FileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                if (!JsonUtils.TryDeserializeFromFile<SessionConfig>(paradigmFilePath, out var config))
                    throw new UserException($"Malformed session config file: {path}");
                if (config.SessionDescriptor == null || config.Paradigm.Id == null)
                    throw new UserException($"Malformed session config file: {path}");
                return new SessionListViewItem(config.SessionDescriptor, new ParadigmItem(path, config.Paradigm));
            }
            throw new UserException($"Unsupported paradigm config file type: {path}");
        }

        private static DeviceItem ReadDeviceConfig(string path, string alternativeDirectory)
        {
            if (!App.FindFile(alternativeDirectory, path, out var deviceFilePath))
                throw new UserException($"Device config file not found: {deviceFilePath}");
            if (path.EndsWith(SessionConfig.FileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                if (!JsonUtils.TryDeserializeFromFile<SessionConfig>(deviceFilePath, out var config))
                    throw new UserException($"Malformed session config file: {path}");
                return new DeviceItem(alternativeDirectory, config.Devices);
            }
            if (path.EndsWith(DeviceFileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                if (!JsonUtils.TryDeserializeFromFile<DeviceConfig[]>(deviceFilePath, out var config))
                    throw new UserException($"Malformed device config file: {path}");
                return new DeviceItem(alternativeDirectory, config);
            }
            throw new UserException($"Unsupported device config file type: {path}");
        }

        public void Start()
        {
            if (string.IsNullOrWhiteSpace(SubjectTextBox.Text))
            {
                MessageBox.Show("Subject name cannot be empty.");
                return;
            }
            if (_sessionListViewItems.IsEmpty())
            {
                MessageBox.Show("Cannot start without sessions.");
                return;
            }
            try
            {
                lock (RunSessionsBtn)
                {
                    if (!RunSessionsBtn.IsEnabled) return;
                    RunSessionsBtn.IsEnabled = false;
                    Visibility = Visibility.Hidden;
                }
                Bootstrap.StartSessions(SubjectTextBox.Text,
                    _sessionListViewItems.Select(session => session.SessionDescriptor).ToArray(),
                    _sessionListViewItems.Select(session => session.Paradigm.Value).ToArray(),
                    DeviceConfigPanel.DeviceConfigs, this);
            }
            catch (Exception ex)
            {
                Logger.Error("Start", ex);
                App.ShowErrorMessage(ex);
            }
            finally
            {
                lock (RunSessionsBtn)
                {
                    RunSessionsBtn.IsEnabled = true;
                    Visibility = Visibility.Visible;
                }
            }
        }

        private void UpdateTitle() => Title = string.IsNullOrWhiteSpace(_msCfgFile) 
            ? "Multi-Session Configuration" 
            : $"Multi-Session Configuration: {_msCfgFile}";

        private void LoadMultiSessionConfig(string path)
        {
            if (path == null)
            {
                Clear();
                return;
            }

            var dir = Path.GetDirectoryName(Path.GetFullPath(path));

            if (!JsonUtils.TryDeserializeFromFile<MultiSessionConfig>(path, out var msCfg))
                throw new UserException($"Malformed multi-session config file: {path}");
            _sessionListViewItems.Clear();

            SubjectTextBox.Text = msCfg.Subject ?? "";

            foreach (var session in msCfg.Sessions ?? EmptyArray<MultiSessionConfig.SessionItem>.Instance) 
                _sessionListViewItems.Add(ReadSessionConfig(session, dir));

            DeviceConfigPanel.DeviceConfigs = msCfg.Devices;

            _msCfgFile = path;
            UpdateTitle();
        }

        private void SaveMultiSessionConfig(string path)
        {
            new MultiSessionConfig
            {
                Subject = SubjectTextBox.Text,
                Sessions = _sessionListViewItems.Select(item => new MultiSessionConfig.SessionItem
                {
                    SessionDescriptor =  item.SessionDescriptor,
                    ParadigmConfigPath = item.Paradigm.FilePath
                }).ToArray(),
                Devices = DeviceConfigPanel.DeviceConfigs
            }.JsonSerializeToFile(path, JsonUtils.PrettyFormat, JsonUtils.DefaultEncoding);
            _msCfgFile = path;
            UpdateTitle();
        }

        private void SaveDeviceConfig(string path) => DeviceConfigPanel.DeviceConfigs.JsonSerializeToFile(path, JsonUtils.PrettyFormat, JsonUtils.DefaultEncoding); 

        private void Clear(bool newFile = false)
        {
            SubjectTextBox.Text = "";
            _sessionListViewItems.Clear();
            DeviceConfigPanel.DeviceConfigs = EmptyArray<DeviceConfig>.Instance;
            if (newFile) _msCfgFile = null;
        }

        private void RunSessions_OnClick(object sender, RoutedEventArgs e) => Start();

        private void Window_OnLoaded(object sender, RoutedEventArgs e)
        {
            DeviceConfigPanel.UpdateDevices();
            LoadMultiSessionConfig(_msCfgFile);
        }

        private void SessionListViewItemsCollectionOnChanged(object sender, NotifyCollectionChangedEventArgs e) => RunSessionsBtn.IsEnabled = _sessionListViewItems.Any();

        private void SessionListView_OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
            // Note that you can have more than one file.
            var files = e.Data.GetData(DataFormats.FileDrop) as string[] ?? EmptyArray<string>.Instance;
            foreach (var file in files)
            {
                if (!file.EndsWith(SessionConfig.FileSuffix)) continue;
                _sessionListViewItems.Add(ReadSessionConfig(file, null));
            }
        }

        private void SessionListView_OnDragOver(object sender, DragEventArgs e)
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }

        private void SessionListView_OnContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (SessionListView.SelectedItems.Count == 0)
            {
                e.Handled = true;
                return;
            }
            var multipleSelection = SessionListView.SelectedItems.Count > 1;
            MoveUpSessionMenuItem.IsEnabled = !multipleSelection;
            MoveDownSessionMenuItem.IsEnabled = !multipleSelection;
        }

        private void NewMultiSessionConfigMenuItem_OnClick(object sender, RoutedEventArgs e) => Clear(true);

        private void OpenMultiSessionConfigMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Multi-Session Config",
                Multiselect = false,
                CheckFileExists = true,
                DefaultExt = MultiSessionConfig.FileSuffix,
                Filter = FileUtils.GetFileFilter("Multi-Session Config File", MultiSessionConfig.FileSuffix),
            };
            if (!string.IsNullOrWhiteSpace(_msCfgFile)) dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(_msCfgFile)) ?? "";
            if (!dialog.ShowDialog(this).Value) return;
            LoadMultiSessionConfig(dialog.FileName);
        }

        private void SaveMultiSessionConfigMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_msCfgFile))
                SaveMultiSessionConfigAsMenuItem_OnClick(sender, e);
            else
                SaveMultiSessionConfig(_msCfgFile);
        }

        private void SaveMultiSessionConfigAsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var defaultFileName = string.IsNullOrWhiteSpace(_msCfgFile) ? "" : Path.GetFileName(_msCfgFile);
            var dialog = new SaveFileDialog
            {
                Title = "Save Multi-Session Config",
                OverwritePrompt = true,
                AddExtension = true,
                FileName = defaultFileName,
                DefaultExt = MultiSessionConfig.FileSuffix,
                Filter = FileUtils.GetFileFilter("Multi-Session Config File", MultiSessionConfig.FileSuffix),
            };
            if (!string.IsNullOrWhiteSpace(_msCfgFile)) dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(_msCfgFile)) ?? "";
            if (!dialog.ShowDialog(this).Value) return;
            SaveMultiSessionConfig(dialog.FileName);
        }

        private void AddParadigmConfigMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Paradigm Config",
                Multiselect = true,
                CheckFileExists = true,
                DefaultExt = SessionConfig.FileSuffix,
                Filter = FileUtils.GetFileFilter("Paradigm Config File", SessionConfig.FileSuffix),
            };
            if (!string.IsNullOrWhiteSpace(_msCfgFile)) dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(_msCfgFile)) ?? "";
            if (!dialog.ShowDialog(this).Value || dialog.FileNames.IsEmpty()) return;
            foreach (var fileName in dialog.FileNames)
                _sessionListViewItems.Add(ReadSessionConfig(fileName, null));
        }

        private void LoadDeviceConfigMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Open Device Config",
                Multiselect = false,
                CheckFileExists = true,
                DefaultExt = DeviceFileSuffix,
                Filter = FileUtils.GetFileFilter("Device Config File", DeviceFileSuffix),
            };
            if (!string.IsNullOrWhiteSpace(_msCfgFile)) dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(_msCfgFile)) ?? "";
            else if (!string.IsNullOrWhiteSpace(_msCfgFile)) dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(_msCfgFile)) ?? "";
            if (!dialog.ShowDialog(this).Value) return;
            DeviceConfigPanel.DeviceConfigs = ReadDeviceConfig(dialog.FileName, null).Value;
        }

        private void SaveDeviceConfigAsMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            var defaultFileName = string.IsNullOrWhiteSpace(_msCfgFile) 
                ? $"device{DeviceFileSuffix}" 
                : Path.GetFileNameWithoutExtension(_msCfgFile) + DeviceFileSuffix;
            var dialog = new SaveFileDialog
            {
                Title = "Save Device Config",
                OverwritePrompt = true,
                AddExtension = true,
                FileName = defaultFileName,
                DefaultExt = DeviceFileSuffix,
                Filter = FileUtils.GetFileFilter("Device Config File", DeviceFileSuffix),
            };
            if (!string.IsNullOrWhiteSpace(_msCfgFile))
                dialog.InitialDirectory = Path.GetDirectoryName(Path.GetFullPath(_msCfgFile)) ?? "";
            if (!dialog.ShowDialog(this).Value) return;
            SaveDeviceConfig(dialog.FileName);
        }

        private void SystemVariablesMenuItem_OnClick(object sender, RoutedEventArgs e) => App.ConfigSystemVariables();

        private void RemoveSessionMenuItem_OnClick(object sender, RoutedEventArgs e) => 
            _sessionListViewItems.RemoveAll(SessionListView.SelectedItems.OfType<SessionListViewItem>().ToList());

        private void MoveSessionUpMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (SessionListView.SelectedIndex > 0 && SessionListView.SelectedIndex < _sessionListViewItems.Count)
                _sessionListViewItems.Move(SessionListView.SelectedIndex, SessionListView.SelectedIndex - 1);
        }

        private void MoveSessionDownMenuItem_OnClick(object sender, RoutedEventArgs e)
        {
            if (SessionListView.SelectedIndex >= 0 && SessionListView.SelectedIndex < _sessionListViewItems.Count - 1)
                _sessionListViewItems.Move(SessionListView.SelectedIndex, SessionListView.SelectedIndex + 1);
        }

        void Bootstrap.ISessionListener.BeforeAllSessionsStart() { }

        void Bootstrap.ISessionListener.BeforeSessionStart(int index, Session session) { }

        void Bootstrap.ISessionListener.AfterSessionCompleted(int index, Session session) { }

        void Bootstrap.ISessionListener.AfterAllSessionsCompleted(Session[] sessions)
        {
            if (IsKillOnFinish) App.Kill();
        }

    }

}
