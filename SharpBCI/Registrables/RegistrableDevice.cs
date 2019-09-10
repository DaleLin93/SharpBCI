using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Devices;

namespace SharpBCI.Registrables
{

    public class RegistrableDevice : ParameterizedRegistrable
    {

        public readonly DeviceType DeviceType;

        [NotNull] public readonly IDeviceFactory Factory;

        public RegistrableDevice(Plugin plugin,  DeviceType deviceType, [NotNull] IDeviceFactory factory) : base(plugin)
        {
            DeviceType = deviceType;
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static ParameterizedEntity CreateParameterizedEntity(RegistrableDevice device, IReadonlyContext context) => new ParameterizedEntity(device?.Identifier, device?.SerializeParams(context));

        public override string Identifier => Factory.DeviceName;

        public override IEnumerable<IParameterDescriptor> Parameters => Factory.Parameters;

        public IDevice NewInstance(IReadonlyContext context) => Factory.Create(context);

    }

}