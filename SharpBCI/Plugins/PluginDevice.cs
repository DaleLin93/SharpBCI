using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions;
using SharpBCI.Extensions.IO.Devices;

namespace SharpBCI.Plugins
{

    public class PluginDevice : ParameterizedRegistrable
    {

        [NotNull] public readonly Type DeviceClass;

        [NotNull] public readonly DeviceAttribute DeviceAttribute;

        [NotNull] public readonly IDeviceFactory Factory;

        internal PluginDevice(Plugin plugin, [NotNull] Type clz, [NotNull] DeviceAttribute attr, [NotNull] IDeviceFactory factory) : base(plugin)
        {
            DeviceClass = clz ?? throw new ArgumentNullException(nameof(clz));
            DeviceAttribute = attr ?? throw new ArgumentNullException(nameof(attr));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static ParameterizedEntity CreateParameterizedEntity(PluginDevice device, IReadonlyContext context) =>
            new ParameterizedEntity(device?.Identifier, device?.SerializeParams(context));

        public override string Identifier => DeviceName;

        public string DeviceName => DeviceAttribute.Name;

        public DeviceType DeviceType => Factory.GetDeviceType(DeviceClass);

        protected override IEnumerable<IParameterDescriptor> AllParameters => Factory.GetParameters(DeviceClass);

        public IDevice NewInstance(IReadonlyContext context) => Factory.Create(DeviceClass, context);

    }

}