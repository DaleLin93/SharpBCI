using MarukoLib.Lang;
using SharpBCI.Extensions;

namespace SharpBCI.Registrables
{

    public class RegistrableAppEntry : IRegistrable
    {

        public readonly Plugin Plugin;

        public readonly IAppEntry Entry;

        public readonly AppEntryAttribute Attribute;

        public RegistrableAppEntry(Plugin plugin, IAppEntry entry)
        {
            Plugin = plugin;
            Entry = entry;
            Attribute = Entry.GetType().GetAppEntryAttribute();
        }

        public string Identifier => Entry.Name;

        public bool IsAutoStart => Attribute?.AutoStart ?? false;

        public override string ToString() => Identifier;

    }

}