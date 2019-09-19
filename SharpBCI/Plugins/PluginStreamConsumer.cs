using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Streamers;

namespace SharpBCI.Plugins
{

    public class PluginStreamConsumer : ParameterizedRegistrable
    {

        [NotNull] public readonly IStreamConsumerFactory Factory;

        internal PluginStreamConsumer(Plugin plugin, IStreamConsumerFactory factory) : base(plugin) =>
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));

        public static ParameterizedEntity CreateParameterizedEntity(PluginStreamConsumer consumer, IReadonlyContext context) => 
            new ParameterizedEntity(consumer?.Identifier, consumer?.SerializeParams(context));

        public override string Identifier => Factory.Name;

        public override IEnumerable<IParameterDescriptor> Parameters => Factory.Parameters;

        public IStreamConsumer NewInstance(Session session, IReadonlyContext context, byte? index) => Factory.Create(session, context, index);

    }

}