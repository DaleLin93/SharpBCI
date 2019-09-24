using MarukoLib.Lang;
using SharpBCI.Core.Experiment;

namespace SharpBCI.Extensions.Windows
{

    public interface IParameterPresentAdapter
    {

        bool CanReset(IParameterDescriptor parameter);

        bool CanCollapse(IGroupDescriptor group, int depth);

        bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter);

        bool IsVisible(IReadonlyContext context, IDescriptor descriptor);

    }

    public interface ISummaryPresentAdapter
    {

        bool IsVisible(IReadonlyContext context, ISummary summary);

    }

}
