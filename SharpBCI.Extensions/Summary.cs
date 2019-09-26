using System;
using MarukoLib.Lang;

namespace SharpBCI.Extensions
{

    public interface ISummary
    {

        string Name { get; }

        object GetValue(IReadonlyContext context, object instance);

    }

    public abstract class AbstractSummary : ISummary
    {

        protected AbstractSummary(string name) => Name = name;

        public string Name { get; }

        public abstract object GetValue(IReadonlyContext context, object instance);

    }

    public class Summary : AbstractSummary
    {

        private readonly Func<IReadonlyContext, object, object> _valueFunc;

        public Summary(string name, Func<IReadonlyContext, object, object> valueFunc) : base(name) => _valueFunc = valueFunc;

        public static Summary FromContext(string name, Func<IReadonlyContext, object> func) => new Summary(name, (p, e) => func(p));

        public static Summary FromInstance(string name, Func<object, object> func) => new Summary(name, (p, e) => func(e));

        public static Summary FromInstance<T>(string name, Func<T, object> func) => new Summary(name, (p, e) => func((T)e));

        public override object GetValue(IReadonlyContext context, object instance) => _valueFunc(context, instance);

    }

}
