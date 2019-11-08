using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.IO.Devices
{

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

        public virtual double DesiredWidth => double.NaN;

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
