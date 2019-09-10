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

    public struct DeviceType : IEquatable<DeviceType>, IRegistrable
    {

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

        public static DeviceType FromType<T>() where T : IDevice => 
            TryGet(typeof(T), out var deviceType) ? deviceType : throw new ArgumentException($"DeviceTypeAttribute not defined in device type '{typeof(T)}'");

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

        public string Identifier => $"{Name}@{BaseType.FullName}";

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

        [NotNull] Type BaseType { get; }

        IReadOnlyCollection<IParameterDescriptor> Parameters { get; }

        IDevice Create(IReadonlyContext context);

    }

    /// <summary>
    /// Device Factory.
    /// </summary>
    /// <typeparam name="T">Target type</typeparam>
    public abstract class DeviceFactory<T> : IDeviceFactory, IParameterPresentAdapter where T : IDevice
    {

        protected DeviceFactory(string deviceName, params IParameterDescriptor[] parameters)
        {
            DeviceName = deviceName;
            Parameters = parameters;
        }

        public string DeviceName { get; }

        public Type BaseType => typeof(T);

        public virtual IReadOnlyCollection<IParameterDescriptor> Parameters { get; }

        public virtual bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => true;

        public virtual bool IsVisible(IReadonlyContext context, IDescriptor descriptor) => true;

        public abstract T Create(IReadonlyContext context);

        IDevice IDeviceFactory.Create(IReadonlyContext context) => Create(context);

    }

}
