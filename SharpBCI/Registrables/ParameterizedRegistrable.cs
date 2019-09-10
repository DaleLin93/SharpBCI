using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions;

namespace SharpBCI.Registrables
{

    public abstract class ParameterizedRegistrable : IRegistrable 
    {

        [CanBeNull] public readonly Plugin Plugin;

        protected ParameterizedRegistrable(Plugin plugin) => Plugin = plugin;

        public abstract string Identifier { get; }

        public abstract IEnumerable<IParameterDescriptor> Parameters { get; } 

        public IDictionary<string, string> SerializeParams(IReadonlyContext context) => Parameters.SerializeParams(context);

        public IContext DeserializeParams(IDictionary<string, string> input) => Parameters.DeserializeParams(input);

        public override string ToString() => Identifier;

    }

}