using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using MarukoLib.IO;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.Logging;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices;
using SharpBCI.Extensions.Experiments;
using SharpBCI.Registrables;

namespace SharpBCI
{

    public class Plugin : IRegistrable
    {

        private static readonly Logger Logger = Logger.GetLogger(typeof(Plugin));

        public readonly Assembly Assembly;

        public ICollection<IAppEntry> AppEntries { get; private set; }

        public ICollection<IExperimentFactory> ExperimentFactories { get; private set; }

        public IDictionary<int, MarkerDefinition> Markers { get; private set; }

        public IDictionary<int, MarkerDefinition> CustomMarkers { get; private set; }

        public ICollection<DeviceType> DeviceTypes { get; private set; }

        public IDictionary<DeviceType, IReadOnlyList<IDeviceFactory>> DeviceFactories { get; private set; }

        private Plugin(Assembly assembly) => Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));

        public static IReadOnlyCollection<Plugin> ScanPlugins(Registries registries, Action<string, Exception> exceptionHandler)
        {
            var plugins = new LinkedList<Plugin>();
            foreach (var file in Directory.GetFiles(Path.GetFullPath(FileUtils.ExecutableDirectory), "SharpBCI.*.Plugin.dll"))
            {
                Logger.Info("ScanPlugins - loading plugin", "assemblyFile", file);
                try
                {
                    var plugin = new Plugin(Assembly.LoadFile(file));
                    plugin.LoadAppEntries();
                    plugin.LoadExperiments();
                    plugin.LoadDeviceTypes();
                    plugin.LoadMarkers();
                    plugins.AddLast(plugin);
                    foreach (var appEntry in plugin.AppEntries)
                        registries.Registry<RegistrableAppEntry>().Register(new RegistrableAppEntry(plugin, appEntry));
                    foreach (var experimentFactory in plugin.ExperimentFactories)
                        registries.Registry<RegistrableExperiment>().Register(new RegistrableExperiment(plugin, experimentFactory));
                    foreach (var deviceType in plugin.DeviceTypes)
                        registries.Registry<DeviceType>().Register(deviceType);
                    registries.Registry<Plugin>().Register(plugin);
                }
                catch (Exception e)
                {
                    Logger.Error("ScanPlugins - failed to load assembly", e, "assemblyFile", file);
                    exceptionHandler(file, e);
                }
            }

            var deviceTypes = registries.Registry<DeviceType>().Registered;
            foreach (var plugin in plugins)
            {
                plugin.LoadDevices(deviceTypes);
                foreach (var deviceType in deviceTypes)
                    foreach (var deviceFactory in plugin.DeviceFactories[deviceType])
                        registries.Registry<RegistrableDevice>().Register(new RegistrableDevice(plugin, deviceType, deviceFactory));

            }
            return plugins;
        }

        private static T Initiate<T>(Type type) => 
            (T)(type.GetNoArgConstructor() ?? throw new ProgrammingException($"No-arg constructor not found: {type.FullName}")).Invoke(EmptyArray<object>.Instance);

        public string Name => Path.GetFileName(Assembly.Location).Trim("SharpBCI.", ".Plugin.dll", StringComparison.OrdinalIgnoreCase);

        public string Identifier => Assembly.FullName;

        public override string ToString() => Name;

        private void LoadAppEntries()
        {
            var appEntries = new LinkedList<IAppEntry>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IAppEntry).IsAssignableFrom(type))
                {
                    appEntries.AddLast(Initiate<IAppEntry>(type));
                    Logger.Info("ScanPlugins - app entry found", "type", type.FullName);
                }
            }
            AppEntries = new ReadOnlyCollection<IAppEntry>(appEntries.ToList());
        }

        private void LoadExperiments()
        {
            var experimentFactories = new LinkedList<IExperimentFactory>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IExperimentFactory).IsAssignableFrom(type))
                { 
                    experimentFactories.AddLast(Initiate<IExperimentFactory>(type));
                    Logger.Info("ScanPlugins - experiment factory found", "type", type.FullName);
                }
            }
            ExperimentFactories = new ReadOnlyCollection<IExperimentFactory>(experimentFactories.ToList());
        }

        private void LoadDeviceTypes()
        {
            var deviceTypes = new LinkedList<DeviceType>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (typeof(IDevice).IsAssignableFrom(type) && DeviceType.TryGet(type, out var deviceType))
                {
                    deviceTypes.AddLast(deviceType);
                    Logger.Info("ScanPlugins - device type found", "type", type.FullName);
                }
            }
            DeviceTypes = new ReadOnlyCollection<DeviceType>(deviceTypes.ToList());
        }

        private void LoadMarkers()
        {
            var globalMarkers = MarkerDefinitions.GlobalMarkers;
            var markers = new Dictionary<int, MarkerDefinition>(globalMarkers);
            foreach (var type in Assembly.GetExportedTypes())
            foreach (var pair in MarkerDefinitions.GetDefinedMarkers(type))
                if (markers.ContainsKey(pair.Key)) throw new Exception($"Duplicated marker in type: {type.FullName}, {markers[pair.Key]} and {pair.Value}");
                else markers[pair.Key] = pair.Value;
            Markers = new ReadOnlyDictionary<int, MarkerDefinition>(markers);
            var customMarkers = new Dictionary<int, MarkerDefinition>(markers);
            foreach (var globalMarker in globalMarkers.Keys)
                customMarkers.Remove(globalMarker);
            CustomMarkers = new ReadOnlyDictionary<int, MarkerDefinition>(customMarkers);
        }

        private void LoadDevices(IReadOnlyCollection<DeviceType> deviceTypes)
        {
            var deviceFactories = new Dictionary<DeviceType, LinkedList<IDeviceFactory>>();
            foreach (var scanningDeviceType in deviceTypes)
                deviceFactories[scanningDeviceType] = new LinkedList<IDeviceFactory>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IDeviceFactory).IsAssignableFrom(type))
                {
                    var factory = Initiate<IDeviceFactory>(type);
                    var valueType = factory.BaseType;
                    var flag = false;
                    foreach (var deviceType in deviceTypes)
                        if (deviceType.BaseType.IsAssignableFrom(valueType))
                        {
                            deviceFactories[deviceType].AddLast(factory);
                            Logger.Info("ScanPlugins - device factory found", "deviceCategory", deviceType.Name, "type", type.FullName);
                            flag = true;
                        }
                    if (!flag) Logger.Warn("ScanPlugins - factory for unknown type device found", "type", type.FullName);
                }
            }
            var dict = new Dictionary<DeviceType, IReadOnlyList<IDeviceFactory>>();
            foreach (var entry in deviceFactories) dict[entry.Key] = new ReadOnlyCollection<IDeviceFactory>(entry.Value.ToList());
            DeviceFactories = new ReadOnlyDictionary<DeviceType, IReadOnlyList<IDeviceFactory>>(dict);
        }

    }

}