using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions;

namespace SharpBCI.Plugins
{

    public class PluginAppEntry : IRegistrable
    {

        [CanBeNull] public readonly Plugin Plugin;

        [NotNull] public readonly IAppEntry Entry;

        [NotNull] public readonly AppEntryAttribute Attribute;

        internal PluginAppEntry(Plugin plugin, IAppEntry entry)
        {
            Plugin = plugin;
            Entry = entry;
            Attribute = entry.GetType().GetAppEntryAttribute();
        }

        public string Identifier => Attribute.Name;

        public bool IsAutoStart => Attribute.AutoStart;

        public override string ToString() => Identifier;

    }

}