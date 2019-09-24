using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using MarukoLib.Lang;
using MarukoLib.UI;
using Newtonsoft.Json;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Experiments;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Experiments.VEP.MAVEP
{

    [Experiment(ExperimentName, typeof(Factory), "1.0")]
    public class MavepExperiment : StagedExperiment.Basic
    {

        public const string ExperimentName = "Miniature Asymmetric Visual Evoked Potentials (Miniature aVEP)";

        public class Configuration
        {

            public class GuiConfig
            {
                
                public struct StimulusSettings : IAutoParameterizedObject, IAutoParamAdapter
                {

                    [AutoParam("Count", Desc = "positive", AdapterType = typeof(StimulusSettings))]
                    public uint Count;

                    [AutoParam("Size", Unit = "dp", AdapterType = typeof(StimulusSettings))]
                    public double Size;

                    [AutoParam("Horizontal Offset", Unit = "dp", AdapterType = typeof(StimulusSettings))]
                    public double HorizontalOffset;

                    [AutoParam("Vertical Offset", Unit = "dp", AdapterType = typeof(StimulusSettings))]
                    public double VerticalOffset;

                    [AutoParam("Tolerance", Unit = "dp", AdapterType = typeof(StimulusSettings))]
                    public double Tolerance;

                    public bool IsValid(FieldInfo field, object value)
                    {
                        switch (field.Name)
                        {
                            case nameof(Count):
                                if (!(value is uint count) || count < 1) return false;
                                break;
                            case nameof(Size):
                                if (!(value is double size) || size <= 0) return false;
                                break;
                            case nameof(Tolerance):
                                if (!(value is double tolerance) || tolerance < 0) return false;
                                break;
                        }
                        return true;
                    }

                }

                public uint BackgroundColor;

                public Fixation FixationPoint;

                public StimulusSettings Stimulus;

            }

            public class TestConfig
            {

                public BaselinePreference Baseline;

                public uint TrialCount;

                public ulong InterStimulusInterval;

            }

            public GuiConfig Gui;

            public TestConfig Test;

        }

        public class Factory : ExperimentFactory<MavepExperiment>
        {

            // Test Config

            private static readonly Parameter<BaselinePreference> Baseline = new Parameter<BaselinePreference>("Baseline", null, null, new BaselinePreference(true, 10000));

            private static readonly Parameter<uint> TrialCount = new Parameter<uint>("Trial Count", null, null, Predicates.Positive, 50);

            private static readonly Parameter<ulong> InterStimulusInterval = new Parameter<ulong>("Inter-Stimulus Interval", "ms", null, 1300);

            // GUI

            private static readonly Parameter<Color> BackgroundColor = new Parameter<Color>("Background Color", Color.Black);

            private static readonly Parameter<Fixation> FixationPoint = new Parameter<Fixation>("Fixation Point", new Fixation(2, Color.Red));

            private static readonly Parameter<Configuration.GuiConfig.StimulusSettings> Stimulus = Parameter<Configuration.GuiConfig.StimulusSettings>.CreateBuilder("Stimulus")
                .SetDefaultValue(new Configuration.GuiConfig.StimulusSettings { Count = 2, Size = 1, HorizontalOffset = 40, VerticalOffset = 0, Tolerance = 0.5})
                .Build();

            public override IReadOnlyCollection<IGroupDescriptor> ParameterGroups => new[]
            {
                new ParameterGroup("Exp. Params", Baseline, TrialCount, InterStimulusInterval),
                new ParameterGroup("User Interface", BackgroundColor, FixationPoint, Stimulus),
            };

            public override IReadOnlyCollection<ISummary> Summaries => new ISummary[]
            {
                ComputationalSummary.FromExperiment<MavepExperiment>("Experiment Duration", experiment => $"{experiment.StageProviders.GetStages().GetDuration().TotalSeconds} s")
            };

            public override MavepExperiment Create(IReadonlyContext context) => new MavepExperiment(new Configuration
            {
                Gui = new Configuration.GuiConfig
                {
                    BackgroundColor = BackgroundColor.Get(context, ColorUtils.ToUIntArgb),
                    FixationPoint = FixationPoint.Get(context),
                    Stimulus = Stimulus.Get(context)
                },
                Test = new Configuration.TestConfig
                {
                    Baseline = Baseline.Get(context),
                    TrialCount = TrialCount.Get(context),
                    InterStimulusInterval = InterStimulusInterval.Get(context),
                }
            });

        }

        private const string MavepGroupName = "mavep";

        [MarkerDefinition(MavepGroupName)]
        public const int StimClearMarker = MarkerDefinitions.CustomMarkerBase + 10;

        [MarkerDefinition(MavepGroupName)]
        public const int LeftStimMarker = MarkerDefinitions.CustomMarkerBase + 11;

        [MarkerDefinition(MavepGroupName)]
        public const int RightStimMarker = MarkerDefinitions.CustomMarkerBase + 12;

        public readonly Configuration Config;

        public MavepExperiment(Configuration configuration) : base(ExperimentName) => Config = configuration;

        public override void Run(Session session) => new MavepTestWindow(session).Show();

        [JsonIgnore]
        protected override IStageProvider[] StageProviders => new IStageProvider[]
        {
            new PreparationStageProvider(),
            new ConditionStageProvider(Config.Test.Baseline.IsAvailable, new BaselineStageProvider(Config.Test.Baseline.Duration)),
            new MarkedStageProvider(MarkerDefinitions.ExperimentStartMarker),
            new DelayStageProvider(1000),
            new MavepStageProvider(Config.Test),
            new MarkedStageProvider(MarkerDefinitions.ExperimentEndMarker),
            new ConditionStageProvider(Config.Test.Baseline.IsAvailable && Config.Test.Baseline.TwoSided, new BaselineStageProvider(Config.Test.Baseline.Duration)),
            new DelayStageProvider(3000)
        };

    }

}
