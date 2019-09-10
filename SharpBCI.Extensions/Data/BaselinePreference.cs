using System.Collections.Generic;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{

    [ParameterizedObject(typeof(Factory))]
    public struct BaselinePreference : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<BaselinePreference>
        {

            public static readonly NamedProperty<bool> TwoSidedProperty = new NamedProperty<bool>("TwoSided");

            private static readonly Parameter<bool> TwoSided = new Parameter<bool>("Two-Sided", true);

            private static readonly Parameter<uint> Duration = new Parameter<uint>("Duration", "ms", "0 - no baseline", null, 10000);

            public override IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter) =>
                parameter.Metadata.Contains(TwoSidedProperty) ? new IParameterDescriptor[] {Duration} : new IParameterDescriptor[] {TwoSided, Duration};

            public override BaselinePreference Create(IParameterDescriptor parameter, IReadonlyContext context) => 
                new BaselinePreference(TwoSidedProperty.GetOrDefault(parameter.Metadata, TwoSided.Get(context)), Duration.Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, BaselinePreference baselinePreference) => new Context
            {
                [TwoSided] = TwoSidedProperty.GetOrDefault(parameter.Metadata, baselinePreference.TwoSided),
                [Duration] = baselinePreference.Duration
            };

        }

        public readonly bool TwoSided;

        public readonly uint Duration;

        public BaselinePreference(bool twoSided, uint duration)
        {
            TwoSided = twoSided;
            Duration = duration;
        }

        public bool IsAvailable => Duration > 0;

    }
}
