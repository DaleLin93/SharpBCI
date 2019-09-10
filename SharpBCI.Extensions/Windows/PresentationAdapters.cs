using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;

namespace SharpBCI.Extensions.Windows
{

    public interface IParameterPresentAdapter
    {

        bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter);

        bool IsVisible(IReadonlyContext context, IDescriptor descriptor);

    }

    public interface ISummaryPresentAdapter
    {

        bool IsVisible(IReadonlyContext context, ISummary summary);

    }

}
