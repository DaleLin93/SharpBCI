using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{

    [ParameterizedObject(typeof(Factory))]
    public struct TrialPreference : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<TrialPreference>
        {

            private static readonly Parameter<uint> Duration = new Parameter<uint>("Duration", Predicates.Positive, 2000);

            private static readonly Parameter<uint> Interval = new Parameter<uint>("Interval", 500);

            public override TrialPreference Create(IParameterDescriptor parameter, IReadonlyContext context) => new TrialPreference(Duration.Get(context), Interval.Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, TrialPreference trialPreference) => new Context 
            {
                [Duration] = trialPreference.Duration,
                [Interval] = trialPreference.Interval
            };

        }

        public readonly uint Duration;

        public readonly uint Interval;

        public TrialPreference(uint duration, uint interval)
        {
            Duration = duration;
            Interval = interval;
        }

    }
}
