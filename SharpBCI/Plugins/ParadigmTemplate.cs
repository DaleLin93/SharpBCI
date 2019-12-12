using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Paradigms;

namespace SharpBCI.Plugins
{

    public class ParadigmTemplate : TemplateAddOn
    {

        [NotNull] public readonly Type Clz;

        [NotNull] public readonly ParadigmAttribute Attribute;

        [NotNull] public readonly IParadigmFactory Factory;

        internal ParadigmTemplate(Plugin plugin, Type clz, ParadigmAttribute attr, IParadigmFactory factory) : base(plugin)
        {
            Clz = clz ?? throw new ArgumentNullException(nameof(clz));
            Attribute = attr ?? throw new ArgumentNullException(nameof(attr));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public override string Identifier => Attribute.Name;

        public string Category => Attribute.Category;

        [CanBeNull] public string Version => Attribute.Version?.ToString();

        protected override IEnumerable<IParameterDescriptor> AllParameters => Factory.GetParameterGroups(Clz).GetAllParameters();

    }

}