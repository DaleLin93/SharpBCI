using System;
using System.Collections.Generic;
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

            /* Parse consumer configurations. */
            var deviceConsumerLists = new Dictionary<DeviceType, IList<Tuple<PluginStreamConsumer, IReadonlyContext>>>();
            foreach (var deviceType in App.Instance.Registries.Registry<PluginDeviceType>().Registered)
            {
                if (!devicePart.TryGetValue(deviceType.DeviceType.Name, out var deviceParams) || deviceParams.Device.Id == null) continue;
                var list = new List<Tuple<PluginStreamConsumer, IReadonlyContext>>();
                deviceConsumerLists[deviceType.DeviceType] = list;
                foreach (var consumerConf in deviceParams.Consumers)
                {
                    if (!App.Instance.Registries.Registry<PluginStreamConsumer>().LookUp(consumerConf.Id, out var pluginStreamConsumer))
                        throw new ArgumentException($"Cannot find specific consumer by id: {consumerConf.Params}");
                    list.Add(new Tuple<PluginStreamConsumer, IReadonlyContext>(pluginStreamConsumer, pluginStreamConsumer.DeserializeParams(consumerConf.Params)));
                }
            }

            /* IMPORTANT: ALL EXPERIMENT RELATED CONFIG SHOULD BE CHECKED BEFORE STEAMERS WERE INITIALIZED */

            var monitorWindow = monitor ? MonitorWindow.Show() : null;

            var sessions = new Session[experimentParts.Count];
            var baseClock = Clock.SystemMillisClock;
            var streamers = CreateStreamerCollection(devicePart, baseClock, out var deviceStreamers);

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
            foreach (var deviceType in App.Instance.Registries.Registry<PluginDeviceType>().Registered)
            {
                if (!devices.TryGetValue(deviceType.DeviceType.Name, out var entity) || entity.Device.Id == null) continue;
                if (!App.Instance.Registries.Registry<PluginDevice>().LookUp(entity.Device.Id, out var device))
                    throw new ArgumentException($"Cannot find device by id: {entity.Device.Id}");
                var deviceInstance = device.NewInstance(device.DeserializeParams(entity.Device.Params));
                var streamer = device.Factory.DeviceType.StreamerFactory?.Create(deviceInstance, clock);
                if (streamer != null) streamers.Add(deviceStreamers[deviceType.DeviceType] = streamer);
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
                experiment = pluginExperiment.Factory.Create(context);
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
