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

        [NotNull] public readonly Type ConsumerClass;

        [NotNull] public readonly StreamConsumerAttribute ConsumerAttribute;

        [NotNull] public readonly IStreamConsumerFactory Factory;

        internal PluginStreamConsumer(Plugin plugin, [NotNull] Type consumerClass, 
            [NotNull] StreamConsumerAttribute consumerAttribute, [NotNull]  IStreamConsumerFactory factory) : base(plugin)
        {
            ConsumerClass = consumerClass ?? throw new ArgumentNullException(nameof(consumerClass));
            ConsumerAttribute = consumerAttribute ?? throw new ArgumentNullException(nameof(consumerAttribute));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static ParameterizedEntity CreateParameterizedEntity(PluginStreamConsumer consumer, IReadonlyContext context) => 
            new ParameterizedEntity(consumer?.Identifier, consumer?.SerializeParams(context));

        public override string Identifier => ConsumerAttribute.Name;

        public override IEnumerable<IParameterDescriptor> AllParameters => Factory.GetParameters(ConsumerClass);

        public IStreamConsumer NewInstance(Session session, IReadonlyContext context, byte? index) => Factory.Create(ConsumerClass, session, context, index);

    }

}