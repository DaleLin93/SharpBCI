using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions;

namespace SharpBCI.Plugins
{

    public abstract class ParameterizedRegistrable : IRegistrable 
    {

        [CanBeNull] public readonly Plugin Plugin;

        protected ParameterizedRegistrable(Plugin plugin) => Plugin = plugin;

        public abstract string Identifier { get; }

        protected abstract IEnumerable<IParameterDescriptor> AllParameters { get; } 

        public IDictionary<string, string> SerializeParams(IReadonlyContext context) => AllParameters.SerializeParams(context);

        public IContext DeserializeParams(IDictionary<string, string> input) => AllParameters.DeserializeParams(input);

        public override string ToString() => Identifier;

    }

}