using System;
using System.Collections.Generic;
using System.Reflection;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.IO;

namespace SharpBCI.Extensions.IO.Devices
{

    public interface IStreamerFactory
    {

        /// <summary>
        /// Gets a value indicating whether the <see cref="IStreamerFactory"/> is device-dependent or not.
        /// If the <see cref="IStreamerFactory"/> is device-dependent, the device parameter must be not null to create a streamer.
        /// </summary>
        bool IsDeviceDependent { get; }

        /// <summary>
        /// The type of streaming value.
        /// </summary>
        [NotNull] Type StreamingType { get; }

        [NotNull] IStreamer Create([CanBeNull] IDevice device, [NotNull] IClock clock);

    }

    public interface IDataVisualizer
    {

        void Visualize([NotNull] IDevice device);

    }

    public struct DeviceType : IEquatable<DeviceType>
    {

        private static readonly IDictionary<Type, DeviceType> DeviceTypes = new Dictionary<Type, DeviceType>();

        public DeviceType([NotNull] string name, [NotNull] string displayName, [NotNull] Type baseType, bool required,
            [CanBeNull] Type streamerFactoryType, [CanBeNull] Type dataVisualizerType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            BaseType = baseType ?? throw new ArgumentNullException(nameof(baseType));
            IsRequired = required;
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
            var attr = type.GetCustomAttribute<DeviceTypeAttribute>();
            if (attr == null)
            {
                deviceType = default;
                return false;
            }
            deviceType = new DeviceType(attr.Name, attr.DisplayName, type, attr.IsRequired, attr.StreamerFactory, attr.DataVisualizer);
            return true;
        }

        [NotNull] public string Name { get; }

        [NotNull] public string DisplayName { get; }

        [NotNull] public Type BaseType { get; }
        
        public bool IsRequired { get; }

        [CanBeNull] public IStreamerFactory StreamerFactory { get; }

        [CanBeNull] public IDataVisualizer DataVisualizer { get; }

        public bool Equals(DeviceType other) => BaseType == other.BaseType;

        public override bool Equals(object obj) => obj is DeviceType other && Equals(other);

        public override int GetHashCode() => Name.GetHashCode();

        public override string ToString() => Name;

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
    public class DeviceTypeAttribute : Attribute
    {

        public DeviceTypeAttribute([NotNull] string displayName) : this(ParameterUtils.GenerateKeyByName(displayName), displayName) { }

        public DeviceTypeAttribute([NotNull] string name, [NotNull] string displayName)
        {
            Name = name;
            DisplayName = displayName;
        }

        [NotNull] public string Name { get; }

        [NotNull] public string DisplayName { get; }

        public bool IsRequired { get; set; }

        [CanBeNull] public Type StreamerFactory { get; set; }

        [CanBeNull] public Type DataVisualizer { get; set; }

    }

    /// <summary>
    /// Device interface.
    /// </summary>
    public interface IDevice : IDisposable
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

        public abstract void Dispose();

    }
    
}
