using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions;

namespace SharpBCI.Plugins
{

    public abstract class TemplateAddOn : IRegistrable 
    {

        [CanBeNull] public readonly Plugin Plugin;

        protected TemplateAddOn(Plugin plugin) => Plugin = plugin;

        public abstract string Identifier { get; }

        protected abstract IEnumerable<IParameterDescriptor> AllParameters { get; } 

        public IDictionary<string, string> SerializeArgs(IReadonlyContext args) => AllParameters.SerializeArgs(args);

        public IContext DeserializeArgs(IDictionary<string, string> args) => AllParameters.DeserializeArgs(args);

        public override string ToString() => Identifier;

    }

}