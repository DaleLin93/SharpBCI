using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions.Devices;

namespace SharpBCI.Plugins
{

    public class PluginDeviceType : IRegistrable
    {

        [CanBeNull] public readonly Plugin Plugin;

        public readonly DeviceType DeviceType;

        internal PluginDeviceType(Plugin plugin, DeviceType deviceType) 
        {
            Plugin = plugin;
            DeviceType = deviceType;
        }

        public string Identifier => $"{DeviceType.Name}@{DeviceType.BaseType.FullName}";

    }

}