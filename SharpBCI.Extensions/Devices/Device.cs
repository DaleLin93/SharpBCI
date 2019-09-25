using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Devices
{

    public interface IStreamerFactory
    {

        [NotNull] Type ValueType { get; }

        [NotNull] IStreamer Create([NotNull] IDevice device, [NotNull] IClock clock);

    }

    public interface IDataVisualizer
    {

        void Visualize([NotNull] IDevice device);

    }

    public struct DeviceType : IEquatable<DeviceType>
    {

        private static readonly IDictionary<Type, DeviceType> DeviceTypes = new Dictionary<Type, DeviceType>();

        public DeviceType([NotNull] string name, [NotNull] string displayName, [NotNull] Type baseType,
            [CanBeNull] Type streamerFactoryType, [CanBeNull] Type dataVisualizerType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            BaseType = baseType ?? throw new ArgumentNullException(nameof(baseType));
            StreamerFactory = (IStreamerFactory)streamerFactoryType?.InitClassOrStruct();
            DataVisualizer = (IDataVisualizer)dataVisualizerType?.InitClassOrStruct();
        }

        public static bool operator ==(DeviceType left, DeviceType right) => left.Equals(right);

        public static bool operator !=(DeviceType left, DeviceType right) => !left.Equals(right);

        public static DeviceType Of(Type type)
        {
            if (DeviceTypes.TryGetValue(type, out var deviceType)) return deviceType;
            if (!TryGet(type, out deviceType)) throw new ArgumentException($"{nameof(DeviceTypeAttribute)} not defined in device type '{type}'");
            return DeviceTypes[type] = deviceType;
        }
            

        public static bool TryGet(Type type, out DeviceType deviceType)
        {
            var attribute = type.GetCustomAttribute<DeviceTypeAttribute>();
            if (attribute == null)
            {
                deviceType = default;
                return false;
            }
            deviceType = new DeviceType(attribute.Name, attribute.DisplayName, type, 
                attribute.StreamerFactoryType, attribute.DataVisualizerType);
            return true;
        }
        
        [NotNull] public string Name { get; }

        [NotNull] public string DisplayName { get; }

        [NotNull] public Type BaseType { get; }

        [CanBeNull] public IStreamerFactory StreamerFactory { get; }

        [CanBeNull] public IDataVisualizer DataVisualizer { get; }

        public bool Equals(DeviceType other) => string.Equals(Name, other.Name);

        public override bool Equals(object obj) => obj is DeviceType other && Equals(other);

        public override int GetHashCode() => Name.GetHashCode();

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class DeviceTypeAttribute : Attribute
    {

        public DeviceTypeAttribute([NotNull] string name) : this(name, name) { }

        public DeviceTypeAttribute([NotNull] string name, [NotNull] string displayName)
        {
            Name = name;
            DisplayName = displayName;
        }

        [NotNull] public string Name { get; }

        [NotNull] public string DisplayName { get; }

        [CanBeNull] public Type StreamerFactoryType { get; set; }

        [CanBeNull] public Type DataVisualizerType { get; set; }

    }

    /// <summary>
    /// Device interface.
    /// </summary>
    public interface IDevice
    {

        void Open();

        void Shutdown();

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class DeviceAttribute : Attribute
    {

        public DeviceAttribute([NotNull] string name, [NotNull] Type factoryType, [CanBeNull] string version = null, [CanBeNull] string versionName = null)
        {
            Name = name.Trim2Null() ?? throw new ArgumentException(nameof(name));
            FactoryType = factoryType ?? throw new ArgumentException(nameof(factoryType));
            Version = version == null ? null : Version.Parse(version);
            VersionName = versionName?.Trim2Null();
        }

        [NotNull] public string Name { get; }

        [NotNull] public Type FactoryType { get; }

        [CanBeNull] public Version Version { get; }

        [CanBeNull] public string VersionName { get; }

        public string FullVersionName
        {
            get
            {
                var versionStr = Version == null ? "un-versioned" : $"v{Version}";
                return VersionName == null ? versionStr : $"{versionStr}-{VersionName}";
            }
        }

        public string Description { get; set; }

    }

    /// <summary>
    /// Abstract device.
    /// </summary>
    public abstract class Device : IDevice
    {

        public abstract void Open();

        public abstract void Shutdown();

    }

    /// <summary>
    /// Device factory interface.
    /// </summary>
    public interface IDeviceFactory
    {

        DeviceType GetDeviceType(Type deviceClass);

        [NotNull] IReadOnlyCollection<IParameterDescriptor> GetParameters(Type deviceClass);

        [NotNull] IDevice Create(Type deviceClass, IReadonlyContext context);

    }

    /// <summary>
    /// Device Factory.
    /// </summary>
    /// <typeparam name="TDevice">Class of device</typeparam>
    /// <typeparam name="TDeviceBaseType">Base type of device</typeparam>
    public abstract class DeviceFactory<TDevice, TDeviceBaseType> : IDeviceFactory, IParameterPresentAdapter 
        where TDevice : TDeviceBaseType where TDeviceBaseType : IDevice
    {

        protected DeviceFactory(params IParameterDescriptor[] parameters)
        {
            Parameters = parameters;
            DeviceType = DeviceType.Of(typeof(TDeviceBaseType));
        }

        public DeviceType DeviceType { get; }

        public virtual IReadOnlyCollection<IParameterDescriptor> Parameters { get; }

        public virtual bool CanReset(IParameterDescriptor parameter) => true;

        public virtual bool CanCollapse(IGroupDescriptor group, int depth) => true;

        public virtual bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => true;

        public virtual bool IsVisible(IReadonlyContext context, IDescriptor descriptor) => true;

        public abstract TDevice Create(IReadonlyContext context);

        DeviceType IDeviceFactory.GetDeviceType(Type deviceClass) => DeviceType;

        IReadOnlyCollection<IParameterDescriptor> IDeviceFactory.GetParameters(Type deviceClass) => Parameters;

        IDevice IDeviceFactory.Create(Type deviceClass, IReadonlyContext context) => Create(context);

    }

}
