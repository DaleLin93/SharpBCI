using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Experiments;

namespace SharpBCI.Registrables
{

    public class RegistrableExperiment : ParameterizedRegistrable
    {

        [NotNull] public readonly IExperimentFactory Factory;

        public RegistrableExperiment(Plugin plugin, IExperimentFactory factory) : base(plugin) => Factory = factory ?? throw new ArgumentNullException(nameof(factory));

        public override string Identifier => Attribute.Name;

        public override IEnumerable<IParameterDescriptor> Parameters => Factory.ParameterGroups.GetAllParameters();

        public ExperimentAttribute Attribute => Factory.ExperimentType.GetExperimentAttribute();

    }

}