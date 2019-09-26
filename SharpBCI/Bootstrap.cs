using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.Persistence;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Devices;
using SharpBCI.Plugins;
using SharpBCI.Windows;

namespace SharpBCI
{

    public class Bootstrap
    {

        public interface ISessionListener
        {

            void BeforeStart(int index, Session session);

            void AfterCompleted(int index, Session session);

            void AfterAllCompleted(Session[] sessions);

        }

        public class SuicideAfterCompletedListener : ISessionListener
        {

            public static readonly SuicideAfterCompletedListener Instance = new SuicideAfterCompletedListener();

            private SuicideAfterCompletedListener() { }

            public void BeforeStart(int index, Session session) { }

            public void AfterCompleted(int index, Session session) { }

            public void AfterAllCompleted(Session[] sessions) => App.Kill();

        }

        public static void StartSession(SessionConfig config, bool monitor = false, ISessionListener sessionListener = null) =>
            StartSession(config.Subject, new[] {config.SessionDescriptor}, new[] {config.Paradigm}, config.Devices, monitor, sessionListener);

        public static void StartSession(string subject, string[] sessionDescriptors, ParameterizedEntity[] paradigms, [NotNull] DeviceParams[] devices, bool monitor = false,
            ISessionListener sessionListener = null)
        {
            subject = subject?.Trim2Null() ?? throw new UserException("subject name cannot be empty");
            if (sessionDescriptors.Length != paradigms.Length) throw new ProgrammingException("The count of session descriptors and the count of paradigms are not equal");
            var sessionNum = sessionDescriptors.Length;

            /* Constructs paradigm instances. */
            var paradigmInstances = new IParadigm[sessionNum];
            var formattedSessionDescriptors = new string[sessionNum];
            for (var i = 0; i < sessionNum; i++)
            {
                var paradigm = paradigms[i];
                if (!App.Instance.Registries.Registry<PluginParadigm>().LookUp(paradigm.Id, out var pluginParadigm))
                    throw new ArgumentException($"Cannot find specific paradigm by id: {paradigm.Id}");
                var paradigmContext = pluginParadigm.DeserializeParams(paradigm.Params);
                if (!TryInitiateParadigm(pluginParadigm, paradigmContext, out var paradigmInstance)) return;
                paradigmInstances[i] = paradigmInstance;
                formattedSessionDescriptors[i] = SessionConfigExt.GetFullSessionName(subject, sessionDescriptors[i], paradigmContext);
            }

            var deviceTypes = App.Instance.Registries.Registry<PluginDeviceType>().Registered.Select(pdt => pdt.DeviceType).ToArray();

            /* Constructs device map. */
            var deviceMap = new Dictionary<string, DeviceParams>();
            foreach (var device in devices)
                if (!deviceMap.ContainsKey(device.DeviceType))
                    deviceMap[device.DeviceType] = device;

            /* Parse consumer configurations. */
            var deviceConsumerLists = new Dictionary<DeviceType, IList<Tuple<PluginStreamConsumer, IReadonlyContext>>>();
            foreach (var deviceType in deviceTypes)
            {
                if (!deviceMap.TryGetValue(deviceType.Name, out var deviceParams) || deviceParams.Device.Id == null) continue;
                var list = new List<Tuple<PluginStreamConsumer, IReadonlyContext>>();
                deviceConsumerLists[deviceType] = list;
                foreach (var consumerConf in deviceParams.Consumers)
                {
                    if (consumerConf.Id == null) continue;
                    if (!App.Instance.Registries.Registry<PluginStreamConsumer>().LookUp(consumerConf.Id, out var pluginStreamConsumer))
                        throw new ArgumentException($"Cannot find specific consumer by id: {consumerConf.Id}");
                    list.Add(new Tuple<PluginStreamConsumer, IReadonlyContext>(pluginStreamConsumer, pluginStreamConsumer.DeserializeParams(consumerConf.Params)));
                }
            }

            /* IMPORTANT: ALL PARADIGM RELATED CONFIG SHOULD BE CHECKED BEFORE STEAMERS WERE INITIALIZED */

            var deviceInstances = InitiateDevices(deviceTypes, deviceMap);

            var monitorWindow = monitor ? MonitorWindow.Show() : null;

            var sessions = new Session[sessionNum];
            var baseClock = Clock.SystemMillisClock;
            var streamers = CreateStreamerCollection(deviceTypes, deviceInstances, baseClock, out var deviceStreamers);

            using (var disposablePool = new DisposablePool())
            {
                try
                {
                    streamers.Start();
                    monitorWindow?.Bind(streamers);

                    for (var i = 0; i < sessionNum; i++)
                    {
                        var session = sessions[i] = new Session(App.Instance.Dispatcher, subject, formattedSessionDescriptors[i], 
                            baseClock, paradigmInstances[i], streamers, App.DataDir);

                        new SessionConfig { Paradigm = paradigms[i], Devices = devices }
                            .JsonSerializeToFile(session.GetDataFileName(SessionConfig.FileSuffix), JsonUtils.PrettyFormat, Encoding.UTF8);

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

                        sessionListener?.BeforeStart(i, session);
                        var result = session.Run();
                        sessionListener?.AfterCompleted(i, session);

                        disposablePool.DisposeAll(); // Release resources.

                        new SessionInfo(session).JsonSerializeToFile(session.GetDataFileName(SessionInfo.FileSuffix), JsonUtils.PrettyFormat, Encoding.UTF8);
                        result?.Save(session);

                        if (session.UserInterrupted && i < sessionNum - 1
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
            }
            sessionListener?.AfterAllCompleted(sessions);
            foreach (var instance in deviceInstances.Values) instance.Dispose();
        }

        public static IDictionary<DeviceType, IDevice> InitiateDevices(IReadOnlyCollection<DeviceType> deviceTypes, IDictionary<string, DeviceParams> devices)
        {
            var deviceLookups = new Dictionary<DeviceType, Tuple<PluginDevice, IReadonlyContext>>();
            foreach (var deviceType in deviceTypes)
            {
                if (!devices.TryGetValue(deviceType.Name, out var entity) || entity.Device.Id == null)
                {
                    if (deviceType.IsRequired) throw new ArgumentException($"Device type '{deviceType.DisplayName}' is required.");
                    continue;
                }
                if (!App.Instance.Registries.Registry<PluginDevice>().LookUp(entity.Device.Id, out var device))
                    throw new ArgumentException($"Cannot find device by id: {entity.Device.Id}");
                deviceLookups[deviceType] = new Tuple<PluginDevice, IReadonlyContext>(device, device.DeserializeParams(entity.Device.Params));
            }
            var deviceInstances = new Dictionary<DeviceType, IDevice>();
            var success = false;
            try
            {
                foreach (var entry in deviceLookups)
                    deviceInstances[entry.Key] = entry.Value.Item1.NewInstance(entry.Value.Item2);
                success = true;
            }
            finally
            {
                if (!success)
                    foreach (var device in deviceInstances.Values)
                        device.Dispose();
            }
            return deviceInstances;
        }

        /// <summary>
        /// Create streamer collection by given device params.
        /// </summary>
        public static StreamerCollection CreateStreamerCollection(DeviceType[] deviceTypes, IDictionary<DeviceType, IDevice> devices, IClock clock,
            out IDictionary<DeviceType, IStreamer> deviceStreamers)
        {
            deviceStreamers = new Dictionary<DeviceType, IStreamer>();
            var streamers = new StreamerCollection();
            foreach (var deviceType in deviceTypes)
            {
                if (!devices.TryGetValue(deviceType, out var instance) || instance == null) continue;
                var streamer = deviceType.StreamerFactory?.Create(instance, clock);
                if (streamer != null) streamers.Add(deviceStreamers[deviceType] = streamer);
            }
            return streamers;
        }

        /// <summary>
        /// Try initiate paradigm under specific context.
        /// </summary>
        public static bool TryInitiateParadigm(PluginParadigm paradigm, IReadonlyContext context, out IParadigm instance, bool msgBox = true)
        {
            instance = null;
            try
            {
                instance = paradigm.Factory.Create(paradigm.ParadigmClass, context);
                return true;
            }
            catch (Exception ex)
            {
                if (msgBox) App.ShowErrorMessage(ex);
                return false;
            }
        }

    }
}
