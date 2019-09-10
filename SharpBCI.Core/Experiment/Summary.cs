using System;
using MarukoLib.Lang;

namespace SharpBCI.Core.Experiment
{

    public interface ISummary
    {

        string Name { get; }

        object GetValue(IReadonlyContext context, IExperiment experiment);

    }

    public abstract class AbstractSummary : ISummary
    {

        protected AbstractSummary(string name) => Name = name;

        public string Name { get; }

        public abstract object GetValue(IReadonlyContext context, IExperiment experiment);

    }

    public class StaticSummary : AbstractSummary
    {

        private readonly object _value;

        public StaticSummary(string name, object value) : base(name) => _value = value;

        public override object GetValue(IReadonlyContext context, IExperiment experiment) => _value;

    }

    public class ComputationalSummary : AbstractSummary
    {

        private readonly Func<IReadonlyContext, IExperiment, object> _valueFunc;

        public ComputationalSummary(string name, Func<IReadonlyContext, IExperiment, object> valueFunc) : base(name) => _valueFunc = valueFunc;

        public static ComputationalSummary FromParams(string name, Func<IReadonlyContext, object> func) => new ComputationalSummary(name, (p, e) => func(p));

        public static ComputationalSummary FromExperiment(string name, Func<IExperiment, object> func) => new ComputationalSummary(name, (p, e) => func(e));

        public static ComputationalSummary FromExperiment<T>(string name, Func<T, object> func) where T : IExperiment => new ComputationalSummary(name, (p, e) => func((T)e));

        public override object GetValue(IReadonlyContext context, IExperiment experiment) => _valueFunc(context, experiment);

    }

}
