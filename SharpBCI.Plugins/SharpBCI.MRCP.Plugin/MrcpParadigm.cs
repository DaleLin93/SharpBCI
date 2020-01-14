using System.Collections.Generic;
using MarukoLib.Lang;
using Newtonsoft.Json;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Paradigms;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.MRCP
{

    [Paradigm(ParadigmName, typeof(Factory), "EEG", "1.0")]
    public class MrcpParadigm : StagedParadigm.Basic
    {

        public const string ParadigmName = "Movement Related Cortical Potentials (MRCP)";

        public class Configuration
        {

            public class TestConfig
            {

                public uint TrialCount;

                /// <summary>
                /// Trial duration in seconds.
                /// </summary>
                public ushort TrialDuration;

                /// <summary>
                /// Inter-stimulus interval in seconds.
                /// </summary>
                public ushort InterStimulusInterval;

            }

            public TestConfig Test;

        }

        public class Factory : ParadigmFactory<MrcpParadigm>
        {

            // Test

            private static readonly Parameter<ushort> TrialCount = new Parameter<ushort>("Trial Count", null, null, Predicates.Positive, 5);

            private static readonly Parameter<ushort> TrialDuration = new Parameter<ushort>("Trial Duration", "s", null, Predicates.Positive, 10);

            private static readonly Parameter<ushort> InterStimulusInterval = new Parameter<ushort>("Inter-Stimulus Interval", "s", null, 2);

            public override IReadOnlyCollection<IGroupDescriptor> ParameterGroups => ScanGroups(typeof(Factory));

            public override bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => !ReferenceEquals(parameter, TrialDuration);

            public override MrcpParadigm Create(IReadonlyContext context) => new MrcpParadigm(new Configuration
            {
                Test = new Configuration.TestConfig
                {
                    TrialCount = TrialCount.Get(context),
                    TrialDuration = 10,
                    InterStimulusInterval = InterStimulusInterval.Get(context),
                },
            });

        }

        [Marker("mrcp:lift")]
        public const int LiftMarker = MarkerDefinitions.CustomMarkerBase + 1;

        public readonly Configuration Config;

        public MrcpParadigm(Configuration configuration) : base(ParadigmName) => Config = configuration;

        public override void Run(Session session) => new MrcpExperimentWindow(session).ShowDialog();

        [JsonIgnore]
        protected override IStageProvider[] StageProviders => new IStageProvider[]
        {
            new PreparationStageProvider(),
            new MarkedStageProvider(MarkerDefinitions.ParadigmStartMarker),
            new MrcpStageProvider(Config.Test), 
            new MarkedStageProvider(MarkerDefinitions.ParadigmEndMarker),
            new DelayStageProvider(3000)
        };

    }

}
