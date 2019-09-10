using System.Collections.Generic;

namespace SharpBCI.Extensions.Patterns
{

    public interface IPattern<in TP, out TV>
    {

        TV Sample(TP samplingPoint);

    }

    public interface ICompositePattern<out T>
    {

        IReadOnlyCollection<T> Patterns { get; }

    }

}
