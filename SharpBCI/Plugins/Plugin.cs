using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reflection;
using MarukoLib.IO;
using MarukoLib.Lang;
using MarukoLib.Logging;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices;
using SharpBCI.Extensions.Experiments;
using SharpBCI.Extensions.Streamers;

namespace SharpBCI.Plugins
{

    public class Plugin : IRegistrable
    {

        public const string PluginFileNamePrefix = "SharpBCI.";

        public const string PluginFileNameSuffix = ".Plugin.dll";

        public const string PluginFileNamePattern = PluginFileNamePrefix + "*" + PluginFileNameSuffix;

        private static readonly Logger Logger = Logger.GetLogger(typeof(Plugin));

        public readonly Assembly Assembly;

        public IDictionary<int, MarkerDefinition> Markers { get; private set; }

        public IDictionary<int, MarkerDefinition> CustomMarkers { get; private set; }

        public ICollection<PluginDeviceType> DeviceTypes { get; private set; }

        public ICollection<PluginAppEntry> AppEntries { get; private set; }

        public ICollection<PluginExperiment> Experiments { get; private set; }

        public IReadOnlyList<PluginDevice> Devices { get; private set; }

        public IReadOnlyList<PluginStreamConsumer> StreamConsumers { get; private set; }

        private Plugin(Assembly assembly)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            Name = Path.GetFileName(Assembly.Location).Trim(PluginFileNamePrefix, PluginFileNameSuffix, StringComparison.OrdinalIgnoreCase);
            Identifier = Assembly.FullName;
        }

        public static PluginExperiment InitPluginExperiment(Plugin plugin, Type type)
        {
            var experimentAttribute = type.GetCustomAttribute<ExperimentAttribute>();
            return new PluginExperiment(plugin, type, experimentAttribute, (IExperimentFactory)Activator.CreateInstance(experimentAttribute.FactoryType));
        }

        public static PluginDevice InitPluginDevice(Plugin plugin, Type type)
        {
            var deviceAttribute = type.GetCustomAttribute<DeviceAttribute>();
            return new PluginDevice(plugin, type, deviceAttribute, (IDeviceFactory)Activator.CreateInstance(deviceAttribute.FactoryType));
        }

        public static PluginStreamConsumer InitPluginStreamConsumer(Plugin plugin, Type type)
        {
            var consumerAttribute = type.GetCustomAttribute<StreamConsumerAttribute>();
            return new PluginStreamConsumer(plugin, type, consumerAttribute, (IStreamConsumerFactory)Activator.CreateInstance(consumerAttribute.FactoryType));
        }

        public static IReadOnlyCollection<Plugin> ScanPlugins(Registries registries, Action<string, Exception> exceptionHandler)
        {
            var plugins = new LinkedList<Plugin>();
            foreach (var file in Directory.GetFiles(Path.GetFullPath(FileUtils.ExecutableDirectory), PluginFileNamePattern))
            {
                Logger.Info("ScanPlugins - loading plugin", "assemblyFile", file);
                try
                {
                    var plugin = new Plugin(Assembly.LoadFile(file));

                    plugins.AddLast(plugin);
                    plugin.LoadMarkers();
                    plugin.LoadDeviceTypes();
                    plugin.LoadAppEntries();
                    plugin.LoadExperiments();
                    plugin.LoadDevices();
                    plugin.LoadStreamConsumers();

                    registries.Registry<Plugin>().Register(plugin);
                    foreach (var deviceType in plugin.DeviceTypes)
                        registries.Registry<PluginDeviceType>().Register(deviceType);
                    foreach (var appEntry in plugin.AppEntries)
                        registries.Registry<PluginAppEntry>().Register(appEntry);
                    foreach (var experiment in plugin.Experiments)
                        registries.Registry<PluginExperiment>().Register(experiment);
                    foreach (var device in plugin.Devices)
                        registries.Registry<PluginDevice>().Register(device);
                    foreach (var streamConsumer in plugin.StreamConsumers)
                        registries.Registry<PluginStreamConsumer>().Register(streamConsumer);
                }
                catch (Exception e)
                {
                    Logger.Error("ScanPlugins - failed to load assembly", e, "assemblyFile", file);
                    exceptionHandler(file, e);
                }
            }
            return plugins;
        }

        public string Name { get; }

        public string Identifier { get; }

        public override string ToString() => Name;

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

        private void LoadDeviceTypes()
        {
            var deviceTypes = new LinkedList<PluginDeviceType>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (typeof(IDevice).IsAssignableFrom(type) && DeviceType.TryGet(type, out var deviceType))
                {
                    deviceTypes.AddLast(new PluginDeviceType(this, deviceType));
                    Logger.Info("ScanPlugins - device type found", "type", type.FullName);
                }
            }
            DeviceTypes = new ReadOnlyCollection<PluginDeviceType>(deviceTypes.ToArray());
        }

        private void LoadAppEntries()
        {
            var appEntries = new LinkedList<PluginAppEntry>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IAppEntry).IsAssignableFrom(type))
                {
                    appEntries.AddLast(new PluginAppEntry(this, (IAppEntry) Activator.CreateInstance(type)));
                    Logger.Info("ScanPlugins - app entry found", "type", type.FullName);
                }
            }
            AppEntries = new ReadOnlyCollection<PluginAppEntry>(appEntries.ToArray());
        }

        private void LoadExperiments()
        {
            var experiments = new LinkedList<PluginExperiment>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IExperiment).IsAssignableFrom(type))
                {
                    var experimentAttribute = type.GetCustomAttribute<ExperimentAttribute>();
                    if (experimentAttribute == null)
                    {
                        Logger.Warn("LoadExperiments - experiment found without 'ExperimentAttribute'", "type", type);
                        continue;
                    }
                    var factory = (IExperimentFactory) Activator.CreateInstance(experimentAttribute.FactoryType);
                    experiments.AddLast(new PluginExperiment(this, type, experimentAttribute, factory));
                    Logger.Info("ScanPlugins - experiment factory found", "type", type.FullName);
                }
            }
            Experiments = new ReadOnlyCollection<PluginExperiment>(experiments.ToArray());
        }

        private void LoadDevices()
        {
            var devices = new LinkedList<PluginDevice>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IDevice).IsAssignableFrom(type))
                {
                    var deviceAttribute = type.GetCustomAttribute<DeviceAttribute>();
                    if (deviceAttribute == null)
                    {
                        Logger.Warn("LoadDevices - device found without 'DeviceAttribute'", "type", type);
                        continue;
                    }
                    var factory = (IDeviceFactory)Activator.CreateInstance(deviceAttribute.FactoryType);
                    devices.AddLast(new PluginDevice(this, type, deviceAttribute, factory));
                    Logger.Info("ScanPlugins - device factory found", "type", type.FullName);
                }
            }
            Devices = new ReadOnlyCollection<PluginDevice>(devices.ToArray());
        }

        private void LoadStreamConsumers()
        {
            var streamConsumers = new LinkedList<PluginStreamConsumer>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IStreamConsumer).IsAssignableFrom(type))
                {
                    var consumerAttribute = type.GetCustomAttribute<StreamConsumerAttribute>();
                    if (consumerAttribute == null)
                    {
                        Logger.Warn("LoadStreamConsumers - stream consumer found without 'StreamConsumerAttribute'", "type", type);
                        continue;
                    }
                    var factory = (IStreamConsumerFactory)Activator.CreateInstance(consumerAttribute.FactoryType);
                    streamConsumers.AddLast(new PluginStreamConsumer(this, type, consumerAttribute, factory));
                    Logger.Info("ScanPlugins - stream consumer factory found", "type", type.FullName);
                }
            }
            StreamConsumers = new ReadOnlyCollection<PluginStreamConsumer>(streamConsumers.ToArray());
        }

    }

}