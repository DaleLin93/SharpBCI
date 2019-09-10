using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Streamers;

namespace SharpBCI.Registrables
{

    public class RegistrableConsumer : ParameterizedRegistrable
    {

        [NotNull] public readonly IConsumerFactory Factory;

        public RegistrableConsumer(Plugin plugin, IConsumerFactory factory) : base(plugin) => Factory = factory ?? throw new ArgumentNullException(nameof(factory));

        public static ParameterizedEntity CreateParameterizedEntity(RegistrableConsumer consumer, IReadonlyContext context) => 
            new ParameterizedEntity(consumer?.Identifier, consumer?.SerializeParams(context));

        public override string Identifier => Factory.ConsumerName;

        public override IEnumerable<IParameterDescriptor> Parameters => Factory.Parameters;

        public IConsumer NewInstance(Session session, IReadonlyContext context, byte? index) => Factory.Create(session, context, index);

    }

}