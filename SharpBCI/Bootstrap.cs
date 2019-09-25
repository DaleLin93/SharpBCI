using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using MarukoLib.Lang;
using MarukoLib.Persistence;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Devices;
using SharpBCI.Extensions.Experiments;
using SharpBCI.Extensions.Streamers;
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
            StartSession(new[] { config.ExperimentPart }, config.DevicePart, monitor, sessionListener);

        public static void StartSession(IReadOnlyList<SessionConfig.Experiment> experimentParts, IDictionary<string, DeviceParams> devicePart, bool monitor = false,
            ISessionListener sessionListener = null)
        {
            /* Constructs experiment instances. */
            var experiments = new IExperiment[experimentParts.Count];
            var formattedSessionDescriptors = new string[experimentParts.Count];
            for (var i = 0; i < experimentParts.Count; i++)
            {
                var expConf = experimentParts[i];
                if (!App.Instance.Registries.Registry<PluginExperiment>().LookUp(expConf.Params.Id, out var pluginExperiment))
                    throw new ArgumentException($"Cannot find specific experiment by id: {expConf.Params.Id}");
                if (!TryInitiateExperiment(pluginExperiment, pluginExperiment.DeserializeParams(expConf.Params.Params), out var experiment)) return;
                experiments[i] = experiment;
                formattedSessionDescriptors[i] = expConf.GetFormattedSessionDescriptor();
            }

            var deviceTypes = App.Instance.Registries.Registry<PluginDeviceType>().Registered.Select(pdt => pdt.DeviceType).ToArray();

            /* Parse consumer configurations. */
            var deviceConsumerLists = new Dictionary<DeviceType, IList<Tuple<PluginStreamConsumer, IReadonlyContext>>>();
            foreach (var deviceType in deviceTypes)
            {
                if (!devicePart.TryGetValue(deviceType.Name, out var deviceParams) || deviceParams.Device.Id == null) continue;
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

            /* IMPORTANT: ALL EXPERIMENT RELATED CONFIG SHOULD BE CHECKED BEFORE STEAMERS WERE INITIALIZED */

            var deviceInstances = InitiateDevices(deviceTypes, devicePart);

            var monitorWindow = monitor ? MonitorWindow.Show() : null;

            var sessions = new Session[experimentParts.Count];
            var baseClock = Clock.SystemMillisClock;
            var streamers = CreateStreamerCollection(deviceTypes, deviceInstances, baseClock, out var deviceStreamers);

            using (var disposablePool = new DisposablePool())
            {
                try
                {
                    streamers.Start();
                    monitorWindow?.Bind(streamers);

                    for (var i = 0; i < experimentParts.Count; i++)
                    {
                        var experimentPart = experimentParts[i];
                        var experiment = experiments[i];
                        var sessionName = formattedSessionDescriptors[i];
                        var session = sessions[i] = new Session(App.Instance.Dispatcher, experimentPart.Subject, sessionName, baseClock, experiment, streamers, App.DataDir);

                        new SessionConfig { ExperimentPart = experimentPart, DevicePart = devicePart }
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
                    if (deviceType.IsRequired) throw new ArgumentException($"Device type '{deviceType.Name}' is required.");
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
        /// Try initiate experiment experiment under specific context.
        /// </summary>
        public static bool TryInitiateExperiment(PluginExperiment pluginExperiment, IReadonlyContext context, out IExperiment experiment, bool msgBox = true)
        {
            experiment = null;
            try
            {
                experiment = pluginExperiment.Factory.Create(null, context);
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
