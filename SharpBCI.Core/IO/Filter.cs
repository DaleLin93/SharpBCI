using System;

namespace SharpBCI.Core.IO
{

    public interface IFilter : IPriorityComponent
    {

        bool Accept(object value);

    }

    public interface IFilter<in T> : IFilter
    {

        bool Accept(T value);

    }

    public abstract class Filter<T> : IFilter<T>
    {

        public Type AcceptType => typeof(T);

        public virtual Priority Priority { get; } = Priority.Normal;

        public abstract bool Accept(T value);

        bool IFilter.Accept(object value) => Accept((T)value);

    }

}
