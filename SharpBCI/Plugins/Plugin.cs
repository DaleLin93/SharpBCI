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
using SharpBCI.Extensions.IO;
using SharpBCI.Extensions.IO.Devices;
using SharpBCI.Extensions.Paradigms;

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

        public ICollection<DeviceTypeAddOn> DeviceTypes { get; private set; }

        public ICollection<AppEntryAddOn> AppEntries { get; private set; }

        public ICollection<ParadigmTemplate> Paradigms { get; private set; }

        public IReadOnlyList<DeviceTemplate> Devices { get; private set; }

        public IReadOnlyList<ConsumerTemplate> Consumers { get; private set; }

        private Plugin(Assembly assembly)
        {
            Assembly = assembly ?? throw new ArgumentNullException(nameof(assembly));
            Name = Path.GetFileName(Assembly.Location).Trim(PluginFileNamePrefix, PluginFileNameSuffix, false, StringComparison.OrdinalIgnoreCase);
            Identifier = Assembly.FullName;

            LoadMarkers();
            LoadDeviceTypes();
            LoadAppEntries();
            LoadParadigms();
            LoadDevices();
            LoadConsumers();
        }

        public static ParadigmTemplate ForParadigmTemplate(Plugin plugin, Type type)
        {
            var attr = type.GetCustomAttribute<ParadigmAttribute>();
            return new ParadigmTemplate(plugin, type, attr, (IParadigmFactory)Activator.CreateInstance(attr.FactoryType));
        }

        public static DeviceTemplate ForDeviceTemplate(Plugin plugin, Type type)
        {
            var attr = type.GetCustomAttribute<DeviceAttribute>();
            return new DeviceTemplate(plugin, type, attr, (IDeviceFactory)Activator.CreateInstance(attr.FactoryType));
        }

        public static ConsumerTemplate ForConsumerTemplate(Plugin plugin, Type type)
        {
            var attr = type.GetCustomAttribute<ConsumerAttribute>();
            return new ConsumerTemplate(plugin, type, attr, (IConsumerFactory)Activator.CreateInstance(attr.FactoryType));
        }

        public static IReadOnlyCollection<Plugin> ScanPlugins(Action<string, Exception> exceptionHandler)
        {
            var plugins = new LinkedList<Plugin>();
            foreach (var file in Directory.GetFiles(Path.GetFullPath(FileUtils.ExecutableDirectory), PluginFileNamePattern))
            {
                Logger.Info("ScanPlugins - loading plugin", "assemblyFile", file);
                try
                {
                    plugins.AddLast(new Plugin(Assembly.UnsafeLoadFrom(file)));
                }
                catch (Exception e)
                {
                    Logger.Error("ScanPlugins - failed to load assembly", e, "assemblyFile", file);
                    exceptionHandler(file, e);
                }
            }
            return plugins.AsReadonly();
        }

        public string Name { get; }

        public string Identifier { get; }

        public override string ToString() => Name;

        public void Register(Registries registries)
        {
            registries.Registry<Plugin>().Register(this);
            registries.Registry<DeviceTypeAddOn>().RegisterAll(DeviceTypes);
            registries.Registry<AppEntryAddOn>().RegisterAll(AppEntries);
            registries.Registry<ParadigmTemplate>().RegisterAll(Paradigms);
            registries.Registry<DeviceTemplate>().RegisterAll(Devices);
            registries.Registry<ConsumerTemplate>().RegisterAll(Consumers);
            MarkerDefinitions.MarkerRegistry.RegisterAll(CustomMarkers.Values);
        }

        public void Unregister(Registries registries)
        {
            registries.Registry<Plugin>().Unregister(this);
            registries.Registry<DeviceTypeAddOn>().UnregisterAll(DeviceTypes);
            registries.Registry<AppEntryAddOn>().UnregisterAll(AppEntries);
            registries.Registry<ParadigmTemplate>().UnregisterAll(Paradigms);
            registries.Registry<DeviceTemplate>().UnregisterAll(Devices);
            registries.Registry<ConsumerTemplate>().UnregisterAll(Consumers);
            MarkerDefinitions.MarkerRegistry.UnregisterAll(CustomMarkers.Values);
        }

        private void LoadMarkers()
        {
            var globalMarkers = MarkerDefinitions.GlobalMarkers;
            var markers = new Dictionary<int, MarkerDefinition>(globalMarkers.AsDictionary());
            foreach (var type in Assembly.GetExportedTypes())
            {
                MarkerDefinitions.GetDefinedMarkers(type, out var namespacesFromType, out var markersFromType);
                foreach (var pair in markersFromType)
                    if (markers.ContainsKey(pair.Key)) throw new Exception($"Duplicated marker in type: {type.FullName}, {markers[pair.Key]} and {pair.Value}");
                    else markers[pair.Key] = pair.Value;
            }
            Markers = new ReadOnlyDictionary<int, MarkerDefinition>(markers);
            var customMarkers = new Dictionary<int, MarkerDefinition>(markers);
            foreach (var globalMarker in globalMarkers.Keys)
                customMarkers.Remove(globalMarker);
            CustomMarkers = new ReadOnlyDictionary<int, MarkerDefinition>(customMarkers);
        }

        private void LoadDeviceTypes()
        {
            var deviceTypes = new LinkedList<DeviceTypeAddOn>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (typeof(IDevice).IsAssignableFrom(type) && DeviceType.TryGet(type, out var deviceType))
                {
                    deviceTypes.AddLast(new DeviceTypeAddOn(this, deviceType));
                    Logger.Info("LoadDeviceTypes - device type found", "type", type.FullName);
                }
            }
            DeviceTypes = new ReadOnlyCollection<DeviceTypeAddOn>(deviceTypes.ToArray());
        }

        private void LoadAppEntries()
        {
            var appEntries = new LinkedList<AppEntryAddOn>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IAppEntry).IsAssignableFrom(type))
                {
                    appEntries.AddLast(new AppEntryAddOn(this, (IAppEntry) Activator.CreateInstance(type)));
                    Logger.Info("LoadAppEntries - app entry found", "type", type.FullName);
                }
            }
            AppEntries = new ReadOnlyCollection<AppEntryAddOn>(appEntries.ToArray());
        }

        private void LoadParadigms()
        {
            var paradigms = new LinkedList<ParadigmTemplate>();
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
                    paradigms.AddLast(new ParadigmTemplate(this, type, attr, factory));
                    Logger.Info("LoadParadigms - paradigm factory found", "type", type.FullName);
                }
            }
            Paradigms = new ReadOnlyCollection<ParadigmTemplate>(paradigms.ToArray());
        }

        private void LoadDevices()
        {
            var devices = new LinkedList<DeviceTemplate>();
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
                    devices.AddLast(new DeviceTemplate(this, type, attr, factory));
                    Logger.Info("LoadDevices - device factory found", "type", type.FullName);
                }
            }
            Devices = new ReadOnlyCollection<DeviceTemplate>(devices.ToArray());
        }

        private void LoadConsumers()
        {
            var consumers = new LinkedList<ConsumerTemplate>();
            foreach (var type in Assembly.GetExportedTypes())
            {
                if (type.IsClass && !type.IsAbstract && typeof(IConsumer).IsAssignableFrom(type))
                {
                    var attr = type.GetCustomAttribute<ConsumerAttribute>();
                    if (attr == null)
                    {
                        Logger.Warn("LoadConsumers - stream consumer found without 'ConsumerAttribute'", "type", type);
                        continue;
                    }
                    var factory = (IConsumerFactory)Activator.CreateInstance(attr.FactoryType);
                    consumers.AddLast(new ConsumerTemplate(this, type, attr, factory));
                    Logger.Info("LoadConsumers - stream consumer factory found", "type", type.FullName);
                }
            }
            Consumers = new ReadOnlyCollection<ConsumerTemplate>(consumers.ToArray());
        }

    }

}