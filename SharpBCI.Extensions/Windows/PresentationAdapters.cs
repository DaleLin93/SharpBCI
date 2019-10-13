using System.Collections.Generic;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Windows
{

    public interface IPresentAdapter
    {

        double DesiredWidth { get; }

    }

    public interface IParameterPresentAdapter : IPresentAdapter
    {

        bool CanReset(IParameterDescriptor parameter);

        bool CanCollapse(IGroupDescriptor group, int depth);

        bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter);

        bool IsVisible(IReadonlyContext context, IDescriptor descriptor);

    }

    public interface ISummaryPresentAdapter : IPresentAdapter
    {

        bool IsVisible(IReadonlyContext context, ISummary summary);

    }

    public static class PresentAdapterExt
    {

        public static double GetPreferredMinWidth(this IEnumerable<IPresentAdapter> presentAdapters)
        {
            double? max = null;
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var adapter in presentAdapters)
            {
                var w = adapter.DesiredWidth;
                if (!double.IsNaN(w) && (max == null || max.Value < w))
                    max = w;
            }
            return max ?? double.NaN;
        }

    }

}
