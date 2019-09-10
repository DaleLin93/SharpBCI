using System;
using System.Collections.Generic;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;

namespace SharpBCI.Extensions.Streamers
{

    public interface IConsumerFactory
    {

        string ConsumerName { get; }

        Type AcceptType { get; }

        IReadOnlyCollection<IParameterDescriptor> Parameters { get; }

        IConsumer Create(Session session, IReadonlyContext context, byte? num);

    }

    /// <summary>
    /// Consumer Factory.
    /// </summary>
    /// <typeparam name="T">Consumer accept type</typeparam>
    public abstract class ConsumerFactory<T> : IConsumerFactory 
    {

        protected ConsumerFactory(string consumerName, params IParameterDescriptor[] parameters)
        {
            ConsumerName = consumerName;
            Parameters = parameters;
        }

        public string ConsumerName { get; }

        public Type AcceptType => typeof(T);

        public virtual IReadOnlyCollection<IParameterDescriptor> Parameters { get; }

        public abstract IConsumer<T> Create(Session session, IReadonlyContext context, byte? num);

        IConsumer IConsumerFactory.Create(Session session, IReadonlyContext context, byte? num) => Create(session, context, num);

    }

}
