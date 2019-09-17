using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using MarukoLib.Lang;
using SharpBCI.Windows;
using SharpBCI.Core.IO;
using SharpBCI.Core.Experiment;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using MarukoLib.IO;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Extensions.Experiments.Rest;
using SharpBCI.Extensions.Streamers;
using SharpBCI.Registrables;
using File = System.IO.File;
using MarukoLib.Logging;
using MarukoLib.Persistence;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices;
using SharpBCI.Extensions.Experiments;
using SharpBCI.Extensions.Experiments.TextDisplay;
using SharpBCI.Extensions.Experiments.Countdown;
using SharpBCI.Extensions.Windows;

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

        public readonly Registries Registries = new Registries();

        static App()
        {
            log4net.Config.XmlConfigurator.Configure();
            SetRealTimePriority();
        }

        public App()
        {
            Instance = this;
            AppDomain.CurrentDomain.UnhandledException += AppDomain_UnhandledException;
            Current.DispatcherUnhandledException += Application_DispatcherUnhandledException;
            if (!Directory.Exists(DataDir)) Directory.CreateDirectory(DataDir);
        }

        public static App Instance { get; private set; }

        public static string SystemVariableFilePath => Path.Combine(FileUtils.ExecutableDirectory, SystemVariableFile);

        public static void SetRealTimePriority()
        {
            Process.GetCurrentProcess().PriorityBoostEnabled = true;
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
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
                    SystemVariables.ParameterDefinitions, SystemVariables.Context)
                { Width = 800 };
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
            }
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public static void StartWithArgs(string[] args)
        {
            if (args.IsEmpty())
                new LauncherWindow().Show();
            else if (args[0].EndsWith(AutoRunConfig.FileSuffix, StringComparison.OrdinalIgnoreCase))
                new AutoRunConfigWindow(args[0]).Show();
            else if (args[0].EndsWith(SessionConfig.FileSuffix, StringComparison.OrdinalIgnoreCase))
            {
                if (!JsonUtils.TryDeserializeFromFile<SessionConfig>(args[0], out var config))
                    throw new IOException($"Failed to load session config file: {args[0]}");
                StartExperiment(config, false, null, session => Kill());
            }
        }

        public static void StartExperiment(SessionConfig config, bool monitor = false, Action<Session> preStartAction = null, Action<Session> completeAction = null) =>
            StartExperiment(new[] { config.ExperimentPart }, config.DevicePart, monitor, preStartAction, sessions => completeAction?.Invoke(sessions[0]));

        public static void StartExperiment(IReadOnlyList<SessionConfig.Experiment> experimentParts, IDictionary<string, DeviceParams> devicePart, bool monitor = false, 
            Action<Session> preStartAction = null, Action<Session[]> completeAction = null)
        {
            /* Constructs experiment instances. */
            var experiments = new IExperiment[experimentParts.Count];
            var formattedSessionDescriptors = new string[experimentParts.Count];
            for (var i = 0; i < experimentParts.Count; i++)
            {
                var expConf = experimentParts[i];
                if (!Instance.Registries.Registry<RegistrableExperiment>().LookUp(expConf.Params.Id, out var registrableExperiment))
                    throw new ArgumentException($"Cannot find specific experiment by id: {expConf.Params.Id}");
                if (!TryInitiateExperiment(registrableExperiment, registrableExperiment.DeserializeParams(expConf.Params.Params), out var experiment)) return;
                experiments[i] = experiment;
                formattedSessionDescriptors[i] = expConf.GetFormattedSessionDescriptor();
            }

            /* Parse consumer configurations. */
            var deviceConsumerLists = new Dictionary<DeviceType, IList<Tuple<RegistrableConsumer, IReadonlyContext>>>();
            foreach (var deviceType in Instance.Registries.Registry<DeviceType>().Registered)
            {
                if (!devicePart.TryGetValue(deviceType.Name, out var deviceParams) || deviceParams.Device.Id == null) continue;
                var list = new List<Tuple<RegistrableConsumer, IReadonlyContext>>();
                deviceConsumerLists[deviceType] = list;
                foreach (var consumerConf in deviceParams.Consumers)
                {
                    if (!Instance.Registries.Registry<RegistrableConsumer>().LookUp(consumerConf.Id, out var registrableConsumer))
                        throw new ArgumentException($"Cannot find specific consumer by id: {consumerConf.Params}");
                    list.Add(new Tuple<RegistrableConsumer, IReadonlyContext>(registrableConsumer, registrableConsumer.DeserializeParams(consumerConf.Params)));
                }
            }

            /* IMPORTANT: ALL EXPERIMENT RELATED CONFIG SHOULD BE CHECKED BEFORE STEAMERS WERE INITIALIZED */

            var sessions = new Session[experimentParts.Count];
            var baseClock = Clock.SystemMillisClock;
            var streamers = CreateStreamerCollection(devicePart, baseClock, out var deviceStreamers);
            var monitorWindow = monitor ? MonitorWindow.Show() : null;
            var disposablePool = new DisposablePool();

            try
            {
                streamers.Start();
                monitorWindow?.Bind(streamers);

                for (var i = 0; i < experimentParts.Count; i++)
                {
                    var experimentPart = experimentParts[i];
                    var experiment = experiments[i];
                    var sessionName = formattedSessionDescriptors[i];
                    var session = sessions[i] = new Session(Instance.Dispatcher, experimentPart.Subject, sessionName, baseClock, experiment, streamers, DataDir);

                    new SessionConfig { ExperimentPart = experimentPart, DevicePart = devicePart }
                        .JsonSerializeToFile(session.GetDataFileName(SessionConfig.FileSuffix), JsonUtils.PrettyFormat, Encoding.UTF8);

                    var writerBaseTime = session.CreateTimestamp;

                    if (ExperimentProperties.RecordMarkers.Get(experiment.Metadata) && streamers.TryFindFirst<MarkerStreamer>(out var markerStreamer))
                    {
                        var markerFileWriter = new MarkerFileWriter(session.GetDataFileName(MarkerFileWriter.FileSuffix), writerBaseTime);
                        disposablePool.Add(markerFileWriter);
                        markerStreamer.Attach(markerFileWriter);
                        disposablePool.Add(new DelegatedDisposable(() => markerStreamer.Detach(markerFileWriter)));
                    }

                    foreach (var entry in deviceConsumerLists)
                        if (deviceStreamers.TryGetValue(entry.Key, out var deviceStreamer))
                        {
                            var deviceConsumerList = entry.Value;
                            var indexed = deviceConsumerList.Count > 1;
                            byte num = 1;

                            foreach (var tuple in deviceConsumerList)
                            {
                                var consumer = tuple.Item1.NewInstance(session, tuple.Item2, indexed ? num++ : (byte?)null);
                                if (consumer is IDisposable disposable) disposablePool.Add(disposable);
                                deviceStreamer.Attach(consumer);
                                disposablePool.Add(new DelegatedDisposable(() => deviceStreamer.Detach(consumer)));
                            }
                        }

                    preStartAction?.Invoke(session);
                    var result = session.Run();

                    disposablePool.DisposeAll(); // Release resources.

                    new SessionInfo(session).JsonSerializeToFile(session.GetDataFileName(SessionInfo.FileSuffix), JsonUtils.PrettyFormat, Encoding.UTF8);
                    result?.Save(session);

                    if (session.UserInterrupted && i < experimentParts.Count - 1
                        && MessageBox.Show("Continue following sessions?", "User Exit", MessageBoxButton.YesNo,
                            MessageBoxImage.Question, MessageBoxResult.No, MessageBoxOptions.None) == MessageBoxResult.No)
                        break;
                }
            }
            finally
            {
                streamers.Stop();
                monitorWindow?.Release(); // Detach session from monitor.
            }

            completeAction?.Invoke(sessions);
        }

        /// <summary>
        /// Create streamer collection by given device params.
        /// </summary>
        public static StreamerCollection CreateStreamerCollection(IDictionary<string, DeviceParams> devices, IClock clock, 
            out IDictionary<DeviceType, IStreamer> deviceStreamers)
        {
            deviceStreamers = new Dictionary<DeviceType, IStreamer>();
            var streamers = new StreamerCollection();
            streamers.Add(new MarkerStreamer(clock));
            foreach (var deviceType in Instance.Registries.Registry<DeviceType>().Registered)
            {
                if (!devices.TryGetValue(deviceType.Name, out var entity) || entity.Device.Id == null) continue;
                if (!Instance.Registries.Registry<RegistrableDevice>().LookUp(entity.Device.Id, out var device))
                    throw new ArgumentException($"Cannot find device by id: {entity.Device.Id}");
                var deviceInstance = device.NewInstance(device.DeserializeParams(entity.Device.Params));
                var streamer = device.DeviceType.StreamerFactory?.Create(deviceInstance, clock);
                if (streamer != null) streamers.Add(deviceStreamers[deviceType] = streamer);
            }
            return streamers;
        }

        /// <summary>
        /// Try initiate experiment experiment under specific context.
        /// </summary>
        public static bool TryInitiateExperiment(RegistrableExperiment registrableExperiment, IReadonlyContext context, out IExperiment experiment, bool msgBox = true)
        {
            experiment = null;
            try
            {
                experiment = registrableExperiment.Factory.Create(context);
                return true;
            }
            catch (Exception ex)
            {
                if (msgBox) ShowErrorMessage(ex);
                return false;
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
                Logger.Warn("UnhandledException - unexpected user operation", "message", ex.Message);
                MessageBox.Show($"{ex.Message}", "An error occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            else
            {
                Logger.Error("UnhandledException", ex, "message", ex.Message);
                MessageBox.Show($"{ex.Message}\n{ex.StackTrace}", "An error occurred", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            e.Handled = true;
        }

        private static void AppDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex) Logger.Error("UnhandledException", ex, "message", ex.Message);
            MessageBox.Show($"{e.ExceptionObject}", "An error occurred", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Logger.Info("OnStartup", "params", e.Args.Join(" "));
            base.OnStartup(e);
            LoadSystemVariables();

            MarukoLib.DirectX.DirectX.CreateIndependentResource();

            Registries.Registry<RegistrableExperiment>().RegisterAll(
                new RegistrableExperiment(null, new RestExperiment.Factory()),
                new RegistrableExperiment(null, new CountdownExperiment.Factory()),
                new RegistrableExperiment(null, new TextDisplayExperiment.Factory()));

            Registries.Registry<DeviceType>().RegisterAll(
                DeviceType.FromType<IBiosignalSampler>(),
                DeviceType.FromType<IEyeTracker>(),
                DeviceType.FromType<IVideoSource>());

            Registries.Registry<RegistrableDevice>().RegisterAll(
                new RegistrableDevice(null, DeviceType.FromType<IEyeTracker>(), new CursorTracker.Factory()),
                new RegistrableDevice(null, DeviceType.FromType<IEyeTracker>(), new GazeFileReader.Factory()),
                new RegistrableDevice(null, DeviceType.FromType<IBiosignalSampler>(), new GenericOscillator.Factory()),
                new RegistrableDevice(null, DeviceType.FromType<IBiosignalSampler>(), new DataFileReader.Factory()),
                new RegistrableDevice(null, DeviceType.FromType<IVideoSource>(), new ScreenCapturer.Factory()));

            Registries.Registry<RegistrableConsumer>().RegisterAll(
                new RegistrableConsumer(null, new BiosignalDataFileWriter.Factory()),
                new RegistrableConsumer(null, new GazePointFileWriter.Factory()),
                new RegistrableConsumer(null, new VideoFramesFileWriter.Factory()));

            Plugin.ScanPlugins(Registries, (file, ex) => ShowErrorMessage(ex, "Failed to load plugin: " + file));

            foreach (var entry in Registries.Registry<RegistrableAppEntry>().Registered)
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
            MarukoLib.DirectX.DirectX.ReleaseIndependentResource();
            base.OnExit(e);
        }

    }

}
