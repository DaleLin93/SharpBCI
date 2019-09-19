using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices;

namespace SharpBCI.Plugins
{

    public class PluginDevice : ParameterizedRegistrable
    {

        [NotNull] public readonly IDeviceFactory Factory;

        internal PluginDevice(Plugin plugin, [NotNull] IDeviceFactory factory) : base(plugin) => 
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));

        public static ParameterizedEntity CreateParameterizedEntity(PluginDevice device, IReadonlyContext context) =>
            new ParameterizedEntity(device?.Identifier, device?.SerializeParams(context));

        public override string Identifier => Factory.DeviceName;

        public DeviceType DeviceType => Factory.DeviceType;

        public override IEnumerable<IParameterDescriptor> Parameters => Factory.Parameters;

        public IDevice NewInstance(IReadonlyContext context) => Factory.Create(context);

    }

}