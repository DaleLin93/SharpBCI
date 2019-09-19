using System;
using System.Collections.Generic;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;

namespace SharpBCI.Extensions.Streamers
{

    /// <summary>
    /// Factory of StreamConsumer.
    /// </summary>
    public interface IStreamConsumerFactory
    {

        string Name { get; }

        Type AcceptType { get; }

        IReadOnlyCollection<IParameterDescriptor> Parameters { get; }

        IStreamConsumer Create(Session session, IReadonlyContext context, byte? num);

    }

    /// <summary>
    /// An abstract implementation IStreamConsumerFactory.
    /// </summary>
    /// <typeparam name="T">Accept type of StreamConsumer </typeparam>
    public abstract class StreamConsumerFactory<T> : IStreamConsumerFactory 
    {

        protected StreamConsumerFactory(string name, params IParameterDescriptor[] parameters)
        {
            Name = name;
            Parameters = parameters;
        }

        public string Name { get; }

        public Type AcceptType => typeof(T);

        public virtual IReadOnlyCollection<IParameterDescriptor> Parameters { get; }

        public abstract IStreamConsumer<T> Create(Session session, IReadonlyContext context, byte? num);

        IStreamConsumer IStreamConsumerFactory.Create(Session session, IReadonlyContext context, byte? num) => Create(session, context, num);

    }

}
