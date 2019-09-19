using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Experiments;

namespace SharpBCI.Plugins
{

    public class PluginExperiment : ParameterizedRegistrable
    {

        [NotNull] public readonly IExperimentFactory Factory;

        [NotNull] public readonly ExperimentAttribute ExperimentAttribute;

        internal PluginExperiment(Plugin plugin, IExperimentFactory factory) : base(plugin)
        {
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
            ExperimentAttribute = factory.ExperimentType.GetExperimentAttribute();
        }

        public override string Identifier => ExperimentAttribute.Name;

        public override IEnumerable<IParameterDescriptor> Parameters => Factory.ParameterGroups.GetAllParameters();

    }

}