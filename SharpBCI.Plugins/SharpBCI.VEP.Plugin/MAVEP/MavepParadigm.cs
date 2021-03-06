﻿using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using MarukoLib.Lang;
using MarukoLib.Lang.Sequence;
using MarukoLib.UI;
using Newtonsoft.Json;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Paradigms;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.VEP.MAVEP
{

    [Paradigm(ParadigmName, typeof(Factory), "EEG", "1.0")]
    public class MavepParadigm : StagedParadigm.Basic
    {

        public const string ParadigmName = "Miniature Asymmetric Visual Evoked Potentials (Miniature aVEP)";

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

        public class Factory : ParadigmFactory<MavepParadigm>
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
                new ParameterGroup("Experimental", Baseline, TrialCount, InterStimulusInterval),
                new ParameterGroup("User Interface", BackgroundColor, FixationPoint, Stimulus),
            };

            public override IReadOnlyCollection<ISummary> Summaries => new ISummary[]
            {
                Summary.FromInstance<MavepParadigm>("Paradigm Duration", paradigm => $"{paradigm.StageProviders.GetStages().GetDuration().TotalSeconds} s")
            };

            public override MavepParadigm Create(IReadonlyContext context) => new MavepParadigm(new Configuration
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

        [Marker(MavepGroupName)]
        public const int StimClearMarker = MarkerDefinitions.CustomMarkerBase + 10;

        [Marker(MavepGroupName)]
        public const int LeftStimMarker = MarkerDefinitions.CustomMarkerBase + 11;

        [Marker(MavepGroupName)]
        public const int RightStimMarker = MarkerDefinitions.CustomMarkerBase + 12;

        public readonly Configuration Config;

        public MavepParadigm(Configuration configuration) : base(ParadigmName) => Config = configuration;

        public override void Run(Session session) => new MavepExperimentWindow(session).Show();

        [JsonIgnore]
        protected override IStageProvider[] StageProviders
        {
            get
            {
                var randomBools = new RandomBools();
                return new IStageProvider[]
                {
                    new PreparationStageProvider(),
                    new ConditionStageProvider(Config.Test.Baseline.IsAvailable, new BaselineStageProvider(Config.Test.Baseline.Duration)),
                    new MarkedStageProvider(MarkerDefinitions.ParadigmStartMarker),
                    new DelayStageProvider(1000),
                    new RepeatingStageProvider.Advanced(index =>
                    {
                        var value = randomBools.Next(); // false - 0, left-right; true - 1, right-left; 
                        var start = new Stage {Marker = MarkerDefinitions.TrialStartMarker};
                        var left = new Stage {Marker = LeftStimMarker, Duration = 25};
                        var right = new Stage {Marker = RightStimMarker, Duration = 25};
                        var blank = new Stage {Marker = StimClearMarker, Duration = 75};
                        var end = new Stage {Marker = MarkerDefinitions.TrialEndMarker};
                        var interval = new Stage {Marker = StimClearMarker, Duration = Config.Test.InterStimulusInterval};
                        var first = value ? right : left;
                        var second = value ? left : right;
                        return new[] {start, first, blank, second, blank, end, interval};
                    }, Config.Test.TrialCount), 
                    new MarkedStageProvider(MarkerDefinitions.ParadigmEndMarker),
                    new ConditionStageProvider(Config.Test.Baseline.IsAvailable && Config.Test.Baseline.TwoSided, new BaselineStageProvider(Config.Test.Baseline.Duration)),
                    new DelayStageProvider(3000)
                };
            }
        }
    }

}
