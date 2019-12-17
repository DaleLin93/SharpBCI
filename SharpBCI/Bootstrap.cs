using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Windows;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.Persistence;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices;
using SharpBCI.Plugins;

namespace SharpBCI
{

    public class Bootstrap
    {

        public interface ISessionListener
        {

            void BeforeAllSessionsStart();

            void BeforeSessionStart(int index, Session session);

            void AfterSessionCompleted(int index, Session session);

            void AfterAllSessionsCompleted(Session[] sessions);

        }

        public class SuicideAfterCompletedListener : ISessionListener
        {

            public static readonly SuicideAfterCompletedListener Instance = new SuicideAfterCompletedListener();

            private SuicideAfterCompletedListener() { }

            public void BeforeAllSessionsStart() { }

            public void BeforeSessionStart(int index, Session session) { }

            public void AfterSessionCompleted(int index, Session session) { }

            public void AfterAllSessionsCompleted(Session[] sessions) => App.Kill();

        }

        public static void StartSession(SessionConfig config, ISessionListener sessionListener = null) =>
            StartSessions(config.Subject, new[] {config.SessionDescriptor}, new[] {config.Paradigm}, config.Devices, sessionListener);

        /// <summary>
        /// Start multiple sessions in one run continuously.
        /// </summary>
        /// <param name="subject">subject name</param>
        /// <param name="sessionDescriptors">descriptors of each session</param>
        /// <param name="paradigms">paradigms of each session</param>
        /// <param name="devices">devices used across sessions</param>
        /// <param name="sessionListener"></param>
        public static void StartSessions(string subject, [NotNull] string[] sessionDescriptors, [NotNull] SerializedObject[] paradigms, [CanBeNull] DeviceConfig[] devices, 
            ISessionListener sessionListener = null)
        {
            subject = subject?.Trim2Null() ?? throw new UserException("subject name cannot be empty");
            if (sessionDescriptors == null) throw new ArgumentNullException(nameof(sessionDescriptors));
            if (paradigms == null) throw new ArgumentNullException(nameof(paradigms));
            if (sessionDescriptors.Length != paradigms.Length) throw new ProgrammingException("The count of session descriptors and the count of paradigms are not equal");
            for (var i = 0; i < sessionDescriptors.Length; i++)
                sessionDescriptors[i] = sessionDescriptors[i]?.Trim2Null() ?? throw new UserException("session descriptor name cannot be empty");
            var sessionNum = sessionDescriptors.Length;

            /* Constructs paradigm instances. */
            var paradigmInstances = new IParadigm[sessionNum];
            var formattedSessionDescriptors = new string[sessionNum];
            for (var i = 0; i < sessionNum; i++)
            {
                var paradigm = paradigms[i];
                if (!App.Instance.Registries.Registry<ParadigmTemplate>().LookUp(paradigm.Id, out var paradigmTemplate))
                    throw new ArgumentException($"Cannot find specific paradigm by id: {paradigm.Id}");
                var paradigmContext = paradigmTemplate.DeserializeArgs(paradigm.Args);
                if (!TryInitiateParadigm(paradigmTemplate, paradigmContext, out var paradigmInstance)) return;
                paradigmInstances[i] = paradigmInstance;
                formattedSessionDescriptors[i] = SessionConfigExt.StringInterpolation(sessionDescriptors[i], paradigmContext);
            }

            var deviceTypes = App.Instance.Registries.Registry<DeviceTypeAddOn>().Registered.Select(pdt => pdt.DeviceType).ToArray();

            /* Constructs device map. */
            var deviceMap = new Dictionary<string, DeviceConfig>();
            if (devices != null)
                foreach (var device in devices)
                    if (!deviceMap.ContainsKey(device.DeviceType))
                        deviceMap[device.DeviceType] = device;

            /* Parse consumer configurations. */
            var deviceConsumerLists = new Dictionary<DeviceType, IList<TemplateWithArgs<ConsumerTemplate>>>();
            foreach (var deviceType in deviceTypes)
            {
                if (!deviceMap.TryGetValue(deviceType.Name, out var deviceArgs)) continue;
                var list = new List<TemplateWithArgs<ConsumerTemplate>>();
                deviceConsumerLists[deviceType] = list;
                foreach (var consumerConf in deviceArgs.Consumers)
                {
                    if (consumerConf.Id == null) continue;
                    if (!App.Instance.Registries.Registry<ConsumerTemplate>().LookUp(consumerConf.Id, out var consumerTemplate))
                        throw new ArgumentException($"Cannot find specific consumer by id: {consumerConf.Id}");
                    list.Add(new TemplateWithArgs<ConsumerTemplate>(consumerTemplate, consumerTemplate.DeserializeArgs(consumerConf.Args)));
                }
            }

            /* IMPORTANT: ALL PARADIGM RELATED CONFIG SHOULD BE CHECKED BEFORE STEAMERS WERE INITIALIZED */

            var deviceInstances = InitiateDevices(deviceTypes, deviceMap);

            var sessions = new Session[sessionNum];
            var baseClock = Clock.SystemMillisClock;
            var streamers = CreateStreamerCollection(deviceTypes, deviceInstances, baseClock, out var deviceStreamers);

            sessionListener?.BeforeAllSessionsStart();

            var disposablePool = new DisposablePool();
            try
            {
                streamers.Start();

                for (var i = 0; i < sessionNum; i++)
                {
                    var session = sessions[i] = new Session(App.Instance.Dispatcher, subject, formattedSessionDescriptors[i], 
                        baseClock, paradigmInstances[i], streamers, App.DataDir);

                    new SessionConfig { Subject = subject, SessionDescriptor = sessionDescriptors[i], Paradigm = paradigms[i], Devices = devices }
                        .JsonSerializeToFile(session.GetDataFileName(SessionConfig.FileSuffix), JsonUtils.PrettyFormat, Encoding.UTF8);

                    foreach (var entry in deviceConsumerLists)
                        if (deviceStreamers.TryGetValue(entry.Key, out var deviceStreamer))
                        {
                            var consumerListOfDevice = entry.Value;
                            var indexed = consumerListOfDevice.Count > 1;
                            byte num = 1;

                            foreach (var consumerWithParams in consumerListOfDevice)
                            {
                                Debug.Assert(consumerWithParams.Template != null, "consumerWithParams.Template != null");
                                var consumer = consumerWithParams.Template.NewInstance(session, consumerWithParams.Args, indexed ? num++ : (byte?)null);
                                disposablePool.AddIfDisposable(consumer);
                                deviceStreamer.AttachConsumer(consumer);
                                disposablePool.Add(new DelegatedDisposable(() => deviceStreamer.DetachConsumer(consumer)));
                            }
                        }

                    sessionListener?.BeforeSessionStart(i, session);
                    var result = session.Run();
                    sessionListener?.AfterSessionCompleted(i, session);

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
                disposablePool.Dispose();
                streamers.Stop();
            }
            foreach (var instance in deviceInstances.Values) instance.Dispose();
            sessionListener?.AfterAllSessionsCompleted(sessions);
        }

        public static IDictionary<DeviceType, IDevice> InitiateDevices(IReadOnlyCollection<DeviceType> deviceTypes, IDictionary<string, DeviceConfig> devices)
        {
            var deviceLookups = new Dictionary<DeviceType, TemplateWithArgs<DeviceTemplate>>();
            foreach (var deviceType in deviceTypes)
            {
                if (!devices.TryGetValue(deviceType.Name, out var entity) || entity.Device.Id == null)
                {
                    if (deviceType.IsRequired) throw new ArgumentException($"Device type '{deviceType.DisplayName}' is required.");
                    continue;
                }
                if (!App.Instance.Registries.Registry<DeviceTemplate>().LookUp(entity.Device.Id, out var deviceTemplate))
                    throw new ArgumentException($"Cannot find device by id: {entity.Device.Id}");
                deviceLookups[deviceType] = TemplateWithArgs<DeviceTemplate>.OfNullable(deviceTemplate, deviceTemplate.DeserializeArgs(entity.Device.Args));
            }
            var deviceInstances = new Dictionary<DeviceType, IDevice>();
            var success = false;
            try
            {
                foreach (var entry in deviceLookups)
                {
                    Debug.Assert(entry.Value.Template != null, "entry.Value.Template != null");
                    deviceInstances[entry.Key] = entry.Value.Template.NewInstance(entry.Value.Args);
                }

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
                // ReSharper disable once RedundantAssignment
                if (!devices.TryGetValue(deviceType, out var instance)) instance = null;
                var streamer = deviceType.StreamerFactory?.Create(instance, clock);
                if (streamer != null) streamers.Add(deviceStreamers[deviceType] = streamer);
            }
            return streamers;
        }

        /// <summary>
        /// Try initiate paradigm under specific context.
        /// </summary>
        public static bool TryInitiateParadigm(ParadigmTemplate paradigm, IReadonlyContext context, out IParadigm instance, bool msgBox = true)
        {
            instance = null;
            try
            {
                instance = paradigm.Factory.Create(paradigm.Clz, context);
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
