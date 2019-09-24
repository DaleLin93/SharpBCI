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

        [NotNull] IStreamer Create(IDevice device, IClock clock);

    }

    public struct DeviceType : IEquatable<DeviceType>
    {

        private static readonly IDictionary<Type, DeviceType> DeviceTypes = new Dictionary<Type, DeviceType>();

        public DeviceType(string name, string displayName, Type baseType, Type streamerFactoryType)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
            BaseType = baseType ?? throw new ArgumentNullException(nameof(baseType));
            StreamerFactoryType = streamerFactoryType;
            StreamerFactory = (IStreamerFactory) streamerFactoryType?.InitClassOrStruct();
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
            deviceType = new DeviceType(attribute.Name, attribute.DisplayName, type, attribute.StreamerFactoryType);
            return true;
        }
        
        [NotNull] public string Name { get; }

        [NotNull] public string DisplayName { get; }

        [NotNull] public Type BaseType { get; }

        [CanBeNull] public Type StreamerFactoryType { get; }

        [CanBeNull] public IStreamerFactory StreamerFactory { get; }

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

    }

    /// <summary>
    /// Device interface.
    /// </summary>
    public interface IDevice
    {

        [NotNull] string Name { get; }

        void Open();

        void Shutdown();

    }

    /// <summary>
    /// Abstract device.
    /// </summary>
    public abstract class Device : IDevice
    {

        protected Device(string name) => Name = name;

        public string Name { get; }

        public abstract void Open();

        public abstract void Shutdown();

    }

    /// <summary>
    /// Device factory interface.
    /// </summary>
    public interface IDeviceFactory
    {

        [NotNull] string DeviceName { get; }

        DeviceType DeviceType { get; }

        IReadOnlyCollection<IParameterDescriptor> Parameters { get; }

        IDevice Create(IReadonlyContext context);

    }

    /// <summary>
    /// Device Factory.
    /// </summary>
    /// <typeparam name="TDevice">Class of device</typeparam>
    /// <typeparam name="TDeviceBaseType">Base type of device</typeparam>
    public abstract class DeviceFactory<TDevice, TDeviceBaseType> : IDeviceFactory, IParameterPresentAdapter 
        where TDevice : TDeviceBaseType where TDeviceBaseType : IDevice
    {

        protected DeviceFactory(string deviceName, params IParameterDescriptor[] parameters)
        {
            DeviceName = deviceName;
            Parameters = parameters;
            DeviceType = DeviceType.Of(typeof(TDeviceBaseType));
        }

        public string DeviceName { get; }

        public DeviceType DeviceType { get; }

        public virtual IReadOnlyCollection<IParameterDescriptor> Parameters { get; }

        public virtual bool CanReset(IParameterDescriptor parameter) => true;

        public virtual bool CanCollapse(IGroupDescriptor group, int depth) => true;

        public virtual bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => true;

        public virtual bool IsVisible(IReadonlyContext context, IDescriptor descriptor) => true;

        public abstract TDevice Create(IReadonlyContext context);

        IDevice IDeviceFactory.Create(IReadonlyContext context) => Create(context);

    }

}
