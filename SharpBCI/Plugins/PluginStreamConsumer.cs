﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions;
using SharpBCI.Extensions.IO;

namespace SharpBCI.Plugins
{

    public class PluginStreamConsumer : ParameterizedRegistrable
    {

        [NotNull] public readonly Type ConsumerClass;

        [NotNull] public readonly StreamConsumerAttribute ConsumerAttribute;

        [NotNull] public readonly IStreamConsumerFactory Factory;

        internal PluginStreamConsumer(Plugin plugin, [NotNull] Type clz, [NotNull] StreamConsumerAttribute attr, [NotNull]  IStreamConsumerFactory factory) : base(plugin)
        {
            ConsumerClass = clz ?? throw new ArgumentNullException(nameof(clz));
            ConsumerAttribute = attr ?? throw new ArgumentNullException(nameof(attr));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public static ParameterizedEntity CreateParameterizedEntity(PluginStreamConsumer consumer, IReadonlyContext context) => 
            new ParameterizedEntity(consumer?.Identifier, consumer?.SerializeParams(context));

        public override string Identifier => ConsumerAttribute.Name;

        protected override IEnumerable<IParameterDescriptor> AllParameters => Factory.GetParameters(ConsumerClass);

        public IStreamConsumer NewInstance(Session session, IReadonlyContext context, byte? index) => Factory.Create(ConsumerClass, session, context, index);

    }

}