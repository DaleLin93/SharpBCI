using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Experiments;

namespace SharpBCI.Plugins
{

    public class PluginExperiment : ParameterizedRegistrable
    {

        [NotNull] public readonly Type ExperimentClass;

        [NotNull] public readonly ExperimentAttribute ExperimentAttribute;

        [NotNull] public readonly IExperimentFactory Factory;

        internal PluginExperiment(Plugin plugin, Type experimentType, ExperimentAttribute experimentAttribute, IExperimentFactory factory) : base(plugin)
        {
            ExperimentClass = experimentType ?? throw new ArgumentNullException(nameof(experimentType));
            ExperimentAttribute = experimentAttribute ?? throw new ArgumentNullException(nameof(experimentAttribute));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public override string Identifier => ExperimentAttribute.Name;

        public override IEnumerable<IParameterDescriptor> AllParameters => Factory.GetParameterGroups(ExperimentClass).GetAllParameters();

    }

}