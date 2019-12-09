using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions;
using SharpBCI.Extensions.IO;

namespace SharpBCI.Plugins
{

    public class ConsumerTemplate : TemplateAddOn
    {

        [NotNull] public readonly Type Clz;

        [NotNull] public readonly ConsumerAttribute Attribute;

        [NotNull] public readonly IConsumerFactory Factory;

        internal ConsumerTemplate(Plugin plugin, [NotNull] Type clz, [NotNull] ConsumerAttribute attr, [NotNull]  IConsumerFactory factory) : base(plugin)
        {
            Clz = clz ?? throw new ArgumentNullException(nameof(clz));
            Attribute = attr ?? throw new ArgumentNullException(nameof(attr));
            Factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        public override string Identifier => Attribute.Name;

        public Type AcceptType => Factory.GetAcceptType(Clz);

        protected override IEnumerable<IParameterDescriptor> AllParameters => Factory.GetParameters(Clz);

        public IConsumer NewInstance(Session session, IReadonlyContext context, byte? index) => Factory.Create(Clz, session, context, index);

    }

}