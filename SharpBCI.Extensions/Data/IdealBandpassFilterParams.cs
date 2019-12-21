using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{

    public class IdealBandpassFilterParams : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<IdealBandpassFilterParams>
        {

            private static readonly Parameter<double> LowCutOffFrequency = new Parameter<double>("Low Cut-off Frequency", 0);

            private static readonly Parameter<double> HighCutOffFrequency = new Parameter<double>("Low Cut-off Frequency", 0);

            public override IdealBandpassFilterParams Create(IParameterDescriptor parameter, IReadonlyContext context) => 
                new IdealBandpassFilterParams(LowCutOffFrequency.Get(context), HighCutOffFrequency.Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, IdealBandpassFilterParams idealBandpassFilterParams) => new Context
            {
                [LowCutOffFrequency] = idealBandpassFilterParams.LowCutOff,
                [HighCutOffFrequency] = idealBandpassFilterParams.HighCutOff,
            };

        }

        public readonly double LowCutOff, HighCutOff;

        public IdealBandpassFilterParams(double lowCutOff, double highCutOff)
        {
            LowCutOff = lowCutOff;
            HighCutOff = highCutOff;
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

        public override string ToString() => $"{LowCutOff:G1}~{HighCutOff:G1}Hz";

    }

}
