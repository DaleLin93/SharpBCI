using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MarukoLib.Lang;
using SharpBCI.Windows;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Security.Principal;
using System.Text;
using MarukoLib.IO;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Extensions.Paradigms.Rest;
using SharpBCI.Plugins;
using File = System.IO.File;
using MarukoLib.Logging;
using MarukoLib.Persistence;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Apps;
using SharpBCI.Extensions.IO.Devices;
using SharpBCI.Extensions.IO.Devices.BiosignalSources;
using SharpBCI.Extensions.IO.Devices.EyeTrackers;
using SharpBCI.Extensions.IO.Devices.MarkerSources;
using SharpBCI.Extensions.IO.Devices.VideoSources;
using SharpBCI.Extensions.Paradigms.TextDisplay;
using SharpBCI.Extensions.Paradigms.Countdown;
using SharpBCI.Extensions.Windows;
using MarukoLib.UI;
using SharpBCI.Extensions.IO.Consumers.Marker;
using SharpBCI.Extensions.IO.Consumers.Sample;
using SharpBCI.Extensions.IO.Consumers.VideoFrame;

namespace SharpBCI
{

    /// <inheritdoc />
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App 
    {

        /// <summary>
        /// Name of data directory.
        /// </summary>
        public const string DataDir = "Data";

        /// <summary>
        /// Name of system variables configuration file.
        /// </summary>
        public const string SystemVariableFile = "sysvar.conf";

        private static readonly Logger Logger = Logger.GetLogger(typeof(App));

        private static readonly StringBuilder ErrorMessageBuilder = new StringBuilder(1024);

        internal readonly Registries Registries = new Registries();

        static App()
        {
            AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
            log4net.Config.XmlConfigurator.Configure();
            SetRealTimePriority();
            ControlUtils.SetupPressEnterToMoveFocusForAllTextbox();
        }

        public App()
        {
            Instance = this;
            Current.DispatcherUnhandledException += Application_DispatcherUnhandledException;
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
        }

        public static App Instance { get; private set; }

        public static string SystemVariableFilePath => Path.Combine(FileUtils.ExecutableDirectory, SystemVariableFile);

        public static bool IsRealtimePriority => Process.GetCurrentProcess().PriorityClass == ProcessPriorityClass.RealTime;

        public static bool IsAdministrator()
        {
            using (var identity = WindowsIdentity.GetCurrent())
                return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        public static void SetRealTimePriority()
        {
            Process.GetCurrentProcess().PriorityBoostEnabled = true;
            Process.GetCurrentProcess().PriorityClass = IsAdministrator() ? ProcessPriorityClass.RealTime : ProcessPriorityClass.High;
        }

        /// <summary>
        /// Load system variables from file.
        /// </summary>
        public static void LoadSystemVariables() => SystemVariables.Deserialize(SystemVariableFilePath);

        /// <summary>
        /// Save system variables to file.
        /// </summary>
        public static void SaveSystemVariables() => SystemVariables.Serialize(SystemVariableFilePath);

        /// <summary>
        /// Open a config window to modify the system variables.
        /// </summary>
        public static void ConfigSystemVariables()
        {
            var configWindow = new ParameterizedConfigWindow("System Variables",
                SystemVariables.ParameterDefinitions, SystemVariables.Context) {Width = 800};
            if (!configWindow.ShowDialog(out var @params)) return;
            SystemVariables.Apply(@params);
            SaveSystemVariables();
        }

        /// <summary>
        /// Run the specific action in STA thread.
        /// </summary>
        /// <param name="action"></param>
        [SuppressMessage("ReSharper", "InconsistentNaming")]
        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        public static void RunSTA(Action action) => Instance.Dispatcher.Invoke(action);

        /// <summary>
        /// Find file in specific directory or executable directory.
        /// </summary>
        /// <param name="extraDir">Extra path</param>
        /// <param name="filePath">Path of file to find.</param>
        /// <param name="result">Found path</param>
        /// <returns></returns>
        public static bool FindFile(string extraDir, string filePath, out string result)
        {
            result = filePath;
            if (File.Exists(result)) return true;
            if (extraDir != null)
            {
                result = Path.Combine(extraDir, filePath);
                if (File.Exists(result)) return true;
            }
            result = Path.Combine(FileUtils.ExecutableDirectory, filePath);
            return File.Exists(result);
        }

        public static void ShowErrorMessage(Exception ex, string message = null)
        {
            if (ex == null) return;
            lock (ErrorMessageBuilder)
            {
                if (message?.Any() ?? false) ErrorMessageBuilder.Append(message);
                if (!(ex is ProgrammingException || ex is UserException))
                {
                    var currentEx = ex;
                    do
                    {
                        if (ErrorMessageBuilder.Length > 0) ErrorMessageBuilder.Append("\n\n");
                        ErrorMessageBuilder.Append("Exception: ").Append(currentEx.Message)
                            .Append("\n").Append("StackTrace: ").Append(currentEx.StackTrace);
                        currentEx = currentEx.InnerException;
                    } while (currentEx != null);
                }
                if (ErrorMessageBuilder.Length == 0) ErrorMessageBuilder.Append(ex.GetType().Name);
                message = ErrorMessageBuilder.ToString();
                ErrorMessageBuilder.Clear();
            }
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void StartWithArgs(string[] args)
        {
            if (args.IsEmpty())
                new LauncherWindow().Show();
            else if (args[0].EndsWith(MultiSessionConfig.FileSuffix, StringComparison.OrdinalIgnoreCase))
                new MultiSessionConfigWindow(args[0]){IsKillOnFinish = true}.Show();
            else if (args[0].EndsWith(SessionConfig.FileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                if (!JsonUtils.TryDeserializeFromFile<SessionConfig>(args[0], out var config))
                    throw new IOException($"Failed to load session config file: {args[0]}");
                Bootstrap.StartSession(config, false, Bootstrap.SuicideAfterCompletedListener.Instance);
            }
        }

        /// <summary>
        /// Kill the process of this application.
        /// </summary>
        public static void Kill() => ProcessUtils.Suicide();

        private static void Application_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            var ex = e.Exception;
            if (ex is UserException || ex is ProgrammingException)
            {
                Logger.Warn("UnhandledException - unexpected user operation", ex);
                MessageBox.Show($"{ex.Message}", "An error occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                Logger.Error("UnhandledException", ex);
                MessageBox.Show($"{ex.Message}\n{ex.StackTrace}", "An error occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            e.Handled = true;
        }

        private static void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex) Logger.Error("UnhandledException", ex);
            MessageBox.Show($"{e.ExceptionObject}", "An error occurred", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.Info("OnStartup", "params", e.Args.Join(" "));
            base.OnStartup(e);
            LoadSystemVariables();

            MarukoLib.DirectX.Direct2D.CreateIndependentResource();

            Registries.Registry<PluginDeviceType>().RegisterAll(
                new PluginDeviceType(null, DeviceType.Of(typeof(IMarkerSource))),
                new PluginDeviceType(null, DeviceType.Of(typeof(IBiosignalSource))),
                new PluginDeviceType(null, DeviceType.Of(typeof(IEyeTracker))),
                new PluginDeviceType(null, DeviceType.Of(typeof(IVideoSource))));

            Registries.Registry<PluginAppEntry>().RegisterAll(
                new PluginAppEntry(null, new FileRenamingToolAppEntry()));

            Registries.Registry<PluginParadigm>().RegisterAll(
                Plugin.InitPluginParadigm(null, typeof(RestParadigm)),
                Plugin.InitPluginParadigm(null, typeof(CountdownParadigm)),
                Plugin.InitPluginParadigm(null, typeof(TextDisplayParadigm)));

            Registries.Registry<PluginDevice>().RegisterAll(
                Plugin.InitPluginDevice(null, typeof(KeyboardTrigger)),
                Plugin.InitPluginDevice(null, typeof(HeartbeatGenerator)),
                Plugin.InitPluginDevice(null, typeof(SerialPortMarkerSource)),
                Plugin.InitPluginDevice(null, typeof(CursorTracker)),
                Plugin.InitPluginDevice(null, typeof(GazeFileReader)),
                Plugin.InitPluginDevice(null, typeof(GenericOscillator)),
                Plugin.InitPluginDevice(null, typeof(DataFileReader)),
                Plugin.InitPluginDevice(null, typeof(ScreenCaptureSource)));

            Registries.Registry<PluginStreamConsumer>().RegisterAll(
                Plugin.InitPluginStreamConsumer(null, typeof(MarkerAsciiFileWriter)),
                Plugin.InitPluginStreamConsumer(null, typeof(BiosignalAsciiFileWriter)),
                Plugin.InitPluginStreamConsumer(null, typeof(BiosignalBinaryFileWriter)),
                Plugin.InitPluginStreamConsumer(null, typeof(GazePointAsciiFileWriter)),
                Plugin.InitPluginStreamConsumer(null, typeof(GazePointBinaryFileWriter)),
                Plugin.InitPluginStreamConsumer(null, typeof(VideoFramesFileWriter)));

            foreach (var plugin in Plugin.ScanPlugins((file, ex) => ShowErrorMessage(ex, "Failed to load plugin: " + file))) plugin.Register(Registries);

            foreach (var entry in Registries.Registry<PluginAppEntry>().Registered)
                if (entry.IsAutoStart)
                    try
                    {
                        entry.Entry.Run();
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("UnhandledException - app entry auto start", ex, "entryId", entry.Identifier);
                        ShowErrorMessage(ex);
                    }

            try
            {
                StartWithArgs(e.Args);
            }
            catch (Exception ex)
            {
                Logger.Error("UnhandledException", ex, "message", ex.Message);
                ShowErrorMessage(ex);
                Kill();
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            SaveSystemVariables();
            MarukoLib.DirectX.Direct2D.ReleaseIndependentResource();
            base.OnExit(e);
        }

    }

}
