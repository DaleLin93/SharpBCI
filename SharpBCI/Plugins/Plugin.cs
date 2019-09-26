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
using SharpBCI.Extensions.Paradigms;
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

        public ICollection<PluginParadigm> Paradigms { get; private set; }

        public IReadOnlyList<PluginDevice> Devices { get; private set; }

        public IReadOnlyList<PluginStreamConsumer> StreamConsumers { get; private set; }

        private Plugin(Assembly assembly)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            Name = Path.GetFileName(Assembly.Location).Trim(PluginFileNamePrefix, PluginFileNameSuffix, StringComparison.OrdinalIgnoreCase);
            Identifier = Assembly.FullName;
        }

        public static PluginParadigm InitPluginParadigm(Plugin plugin, Type type)
        {
            var attr = type.GetCustomAttribute<ParadigmAttribute>();
            return new PluginParadigm(plugin, type, attr, (IParadigmFactory)Activator.CreateInstance(attr.FactoryType));
        }

        public static PluginDevice InitPluginDevice(Plugin plugin, Type type)
        {
            var attr = type.GetCustomAttribute<DeviceAttribute>();
            return new PluginDevice(plugin, type, attr, (IDeviceFactory)Activator.CreateInstance(attr.FactoryType));
        }

        public static PluginStreamConsumer InitPluginStreamConsumer(Plugin plugin, Type type)
        {
            var attr = type.GetCustomAttribute<StreamConsumerAttribute>();
            return new PluginStreamConsumer(plugin, type, attr, (IStreamConsumerFactory)Activator.CreateInstance(attr.FactoryType));
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
                    plugin.LoadParadigms();
                    plugin.LoadDevices();
                    plugin.LoadStreamConsumers();

                    registries.Registry<Plugin>().Register(plugin);
                    foreach (var deviceType in plugin.DeviceTypes)
                        registries.Registry<PluginDeviceType>().Register(deviceType);
                    foreach (var appEntry in plugin.AppEntries)
                        registries.Registry<PluginAppEntry>().Register(appEntry);
                    foreach (var paradigm in plugin.Paradigms)
                        registries.Registry<PluginParadigm>().Register(paradigm);
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
                    Logger.Info("LoadDeviceTypes - device type found", "type", type.FullName);
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
                    Logger.Info("LoadAppEntries - app entry found", "type", type.FullName);
                }
            }
            AppEntries = new ReadOnlyCollection<PluginAppEntry>(appEntries.ToArray());
        }

        private void LoadParadigms()
        {
            var paradigms = new LinkedList<PluginParadigm>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IParadigm).IsAssignableFrom(type))
                {
                    var attr = type.GetCustomAttribute<ParadigmAttribute>();
                    if (attr == null)
                    {
                        Logger.Warn("LoadParadigms - paradigm found without 'ParadigmAttribute'", "type", type);
                        continue;
                    }
                    var factory = (IParadigmFactory) Activator.CreateInstance(attr.FactoryType);
                    paradigms.AddLast(new PluginParadigm(this, type, attr, factory));
                    Logger.Info("LoadParadigms - paradigm factory found", "type", type.FullName);
                }
            }
            Paradigms = new ReadOnlyCollection<PluginParadigm>(paradigms.ToArray());
        }

        private void LoadDevices()
        {
            var devices = new LinkedList<PluginDevice>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IDevice).IsAssignableFrom(type))
                {
                    var attr = type.GetCustomAttribute<DeviceAttribute>();
                    if (attr == null)
                    {
                        Logger.Warn("LoadDevices - device found without 'DeviceAttribute'", "type", type);
                        continue;
                    }
                    var factory = (IDeviceFactory)Activator.CreateInstance(attr.FactoryType);
                    devices.AddLast(new PluginDevice(this, type, attr, factory));
                    Logger.Info("LoadDevices - device factory found", "type", type.FullName);
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
                    var attr = type.GetCustomAttribute<StreamConsumerAttribute>();
                    if (attr == null)
                    {
                        Logger.Warn("LoadStreamConsumers - stream consumer found without 'StreamConsumerAttribute'", "type", type);
                        continue;
                    }
                    var factory = (IStreamConsumerFactory)Activator.CreateInstance(attr.FactoryType);
                    streamConsumers.AddLast(new PluginStreamConsumer(this, type, attr, factory));
                    Logger.Info("LoadStreamConsumers - stream consumer factory found", "type", type.FullName);
                }
            }
            StreamConsumers = new ReadOnlyCollection<PluginStreamConsumer>(streamConsumers.ToArray());
        }

    }

}