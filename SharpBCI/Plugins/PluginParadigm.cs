using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Paradigms;

namespace SharpBCI.Plugins
{

    public class PluginParadigm : ParameterizedRegistrable
    {

        [NotNull] public readonly Type ParadigmClass;

        [NotNull] public readonly ParadigmAttribute ParadigmAttribute;

        [NotNull] public readonly IParadigmFactory Factory;

        internal PluginParadigm(Plugin plugin, Type clz, ParadigmAttribute attr, IParadigmFactory factory) : base(plugin)
        {
            ParadigmClass = clz ?? throw new ArgumentNullException(nameof(clz));
            ParadigmAttribute = attr ?? throw new ArgumentNullException(nameof(attr));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public override string Identifier => ParadigmAttribute.Name;

        protected override IEnumerable<IParameterDescriptor> AllParameters => Factory.GetParameterGroups(ParadigmClass).GetAllParameters();

    }

}