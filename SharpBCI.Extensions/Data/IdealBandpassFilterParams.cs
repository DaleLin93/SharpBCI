using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{

    public class IdealBandpassFilterParams : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<IdealBandpassFilterParams>
        {

            private static readonly Parameter<double> LowCutoffFrequency = new Parameter<double>("Low Cut-off Frequency", 0);

            private static readonly Parameter<double> HighCutoffFrequency = new Parameter<double>("Low Cut-off Frequency", 0);

            public override IdealBandpassFilterParams Create(IParameterDescriptor parameter, IReadonlyContext context) => 
                new IdealBandpassFilterParams(LowCutoffFrequency.Get(context), HighCutoffFrequency.Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, IdealBandpassFilterParams idealBandpassFilterParams) => new Context
            {
                [LowCutoffFrequency] = idealBandpassFilterParams.LowCutoff,
                [HighCutoffFrequency] = idealBandpassFilterParams.HighCutoff,
            };

        }

        public readonly double LowCutoff, HighCutoff;

        public IdealBandpassFilterParams(double lowCutoff, double highCutoff)
        {
            LowCutoff = lowCutoff;
            HighCutoff = highCutoff;
        }

        public static IdealBandpassFilterParams[] Parse(MatrixQuery matrixQuery)
        {
            var matrix = matrixQuery?.GetMatrix();
            if (matrix == null || matrix.Length == 0) return null;
            var filterCount = matrix.GetRowCount();
            var bandpassFilters = new IdealBandpassFilterParams[filterCount];
            for (var r = 0; r < filterCount; r++)
                bandpassFilters[r] = new IdealBandpassFilterParams(matrix[r, 0], matrix[r, 1]);
            return bandpassFilters;
        }

        public override string ToString() => $"{LowCutoff:G1}~{HighCutoff:G1}Hz";

    }

}
