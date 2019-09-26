using MarukoLib.Lang;
using Newtonsoft.Json;
using SharpBCI.Core.Staging;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Patterns;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.VEP.SSVEP
{

    public static class SsvepParadigmExt
    {

        public static int GetParadigmPatternMultiplier(this SsvepParadigm.Configuration.TestConfig.StimulationParadigm paradigm)
        {
            switch (paradigm)
            {
                case SsvepParadigm.Configuration.TestConfig.StimulationParadigm.Flicker:
                    return 1;
                case SsvepParadigm.Configuration.TestConfig.StimulationParadigm.DualFlickers:
                    return 2;
                default:
                    throw new NotSupportedException();
            }
        }

    }

    [Paradigm(ParadigmName, typeof(Factory), "1.0")]
    public class SsvepParadigm : StagedParadigm.Basic
    {

        public const string ParadigmName = "Steady-State Visual Evoked Potentials (SSVEP)";

        public class Configuration
        {

            public class GuiConfig
            {

                public int Screen;

                public uint BackgroundColor;

                public RectangleLayout BlockSize;

                public Dimensions BlockLayout;

                public Position2D BlockPosition;

                public uint BlockMargin;

                public Border BlockBorder;

                public Colors BlockColors;

                public Fixation BlockFixationPoint;

            }

            public class TestConfig
            {

                public enum StimulationParadigm
                {
                    Flicker, DualFlickers
                }

                public StimulationParadigm Paradigm;

                public ITemporalPattern[] Patterns;

                public BaselinePreference Baseline;

                public ulong TrialDuration;

                public uint TrialCount;

                public ulong InterStimulusInterval;

            }

            public GuiConfig Gui;

            public TestConfig Test;

        }

        public class Factory : ParadigmFactory<SsvepParadigm>
        {

            // Test Config

            private static readonly Parameter<Configuration.TestConfig.StimulationParadigm> Paradigm = Parameter<Configuration.TestConfig.StimulationParadigm>.OfEnum("Paradigm");
            
            private static readonly Parameter<string> Patterns = Parameter<string>.CreateBuilder("Patterns")
                .SetDescription("Available patterns is required, patterns are ordered by priority").SetUnit("Hz@π")
                .SetDefaultValue("14@0,20@0; 15@0,21@0; 16@0,22@0; 17@0,23@0")
                .Build();
            
            private static readonly Parameter<BaselinePreference> Baseline = new Parameter<BaselinePreference>("Baseline", null, null, new BaselinePreference(true, 10000));

            private static readonly Parameter<ulong> TrialDuration = new Parameter<ulong>("Trial Duration", "ms", null, 5000);

            private static readonly Parameter<uint> TrialCount = new Parameter<uint>("Trial Count", null, null, Predicates.Positive, 50);

            private static readonly Parameter<ulong> InterStimulusInterval = new Parameter<ulong>("Inter-Stimulus Interval", "ms", null, 1300);

            // GUI

            private static readonly Parameter<ScreenInfo> Screen = Parameter<ScreenInfo>.CreateBuilder("Screen").SetSelectableValues(ScreenInfo.All, true).Build();

            private static readonly Parameter<Color> BackgroundColor = new Parameter<Color>("Background Color", Color.Black);

            private static readonly Parameter<RectangleLayout> BlockSize = Parameter<RectangleLayout>.CreateBuilder("Block Size")
                .SetDefaultValue(new RectangleLayout(300, 10))
                .SetMetadata(RectangleLayout.Factory.SquareProperty, false)
                .SetMetadata(RectangleLayout.Factory.UnifyMarginProperty, false)
                .Build();

            private static readonly Parameter<Dimensions> BlockLayout = Parameter<Dimensions>.CreateBuilder("Block Layout")
                .SetDefaultValue(new Dimensions(1, 1))
                .SetMetadata(Dimensions.Factory.DimensionNamesProperty, new[] { "Rows", "Cols" })
                .SetValidator(dims => dims.Volume < 1024)
                .Build();

            private static readonly Parameter<Position2D> BlockPosition = new Parameter<Position2D>("Block Position", Position2D.CenterMiddle);

            private static readonly Parameter<Border> BlockBorder = new Parameter<Border>("Block Border", new Border(1, Color.White));

            private static readonly Parameter<Colors> BlockColors = Parameter<Colors>.CreateBuilder("Block Colors")
                .SetDefaultValue(new Colors(Color.Black, Color.White))
                .SetMetadata(Colors.Factory.ColorKeysProperty, new []{"Normal", "Flashing"})
                .Build();

            private static readonly Parameter<Fixation> BlockFixationPoint = new Parameter<Fixation>("Block Fixation Point", new Fixation(2, Color.Red));

            private static ITemporalPattern[] ParseMultiple(string expression)
            {
                var strArray = expression.Contains(';') ? expression.Split(';') : new[] { expression };
                if (strArray.IsEmpty()) return EmptyArray<ITemporalPattern>.Instance;
                var schemes = new ITemporalPattern[strArray.Length];
                for (var i = 0; i < strArray.Length; i++)
                    schemes[i] = Parse(strArray[i].Trim());
                return schemes;
            }

            private static ITemporalPattern Parse(string expression)
            {
                var strArray = expression.Contains(',') ? expression.Split(',') : new[] {expression};
                if (strArray.IsEmpty()) throw new ArgumentException(expression);
                var patterns = new ITemporalPattern[strArray.Length];
                for (var i = 0; i < strArray.Length; i++)
                {
                    var subExp = strArray[i];
                    patterns[i] = subExp.Contains('~') ? (ITemporalPattern) TimeVaryingCosinusoidalPattern.Parse(subExp) : CosinusoidalPattern.Parse(subExp);
                }
                switch (patterns.Length)
                {
                    case 0:
                        return null;
                    case 1:
                        return patterns[0];
                    default:
                        return new CompositeTemporalPattern<ITemporalPattern>(patterns);
                }
            }

            public override IReadOnlyCollection<IGroupDescriptor> ParameterGroups => new[]
            {
                new ParameterGroup("Display", Screen),
                new ParameterGroup("Paradigm Params", Paradigm, Patterns, Baseline, TrialDuration, TrialCount, InterStimulusInterval),
                new ParameterGroup("UI Basic", BackgroundColor),
                new ParameterGroup("UI Block", BlockSize, BlockLayout, BlockPosition, BlockBorder, BlockColors, BlockFixationPoint),
            };

            public override IReadOnlyCollection<ISummary> Summaries => new ISummary[]
            {
                Summary.FromInstance<SsvepParadigm>("Paradigm Duration", paradigm => $"{paradigm.StageProviders.GetStages().GetDuration().TotalSeconds} s")
            };

            public override ValidationResult CheckValid(IReadonlyContext context, IParameterDescriptor parameter)
            {
                if (ReferenceEquals(Patterns, parameter))
                {
                    var patterns = ParseMultiple(Patterns.Get(context));
                    if ((int) BlockLayout.Get(context).Volume * Paradigm.Get(context).GetParadigmPatternMultiplier() > (patterns?.Length ?? 0))
                        return ValidationResult.Failed("Input number of 'Pattern' value must not less than block count * paradigm multiplier");
                }
                return base.CheckValid(context, parameter);
            }

            public override SsvepParadigm Create(IReadonlyContext context) => new SsvepParadigm(new Configuration
            {
                Gui = new Configuration.GuiConfig
                {
                    Screen = Screen.Get(context)?.Index ?? -1,
                    BackgroundColor = BackgroundColor.Get(context, ColorUtils.ToUIntArgb),
                    BlockSize = BlockSize.Get(context),
                    BlockLayout = BlockLayout.Get(context),
                    BlockPosition = BlockPosition.Get(context),
                    BlockBorder = BlockBorder.Get(context),
                    BlockColors = BlockColors.Get(context),
                    BlockFixationPoint = BlockFixationPoint.Get(context)
                },
                Test = new Configuration.TestConfig
                {
                    Paradigm = Paradigm.Get(context),
                    Patterns = Patterns.Get(context, ParseMultiple),
                    Baseline = Baseline.Get(context),
                    TrialDuration = TrialDuration.Get(context),
                    TrialCount = TrialCount.Get(context),
                    InterStimulusInterval = InterStimulusInterval.Get(context),
                }
            });

        }

        public readonly Configuration Config;

        public SsvepParadigm(Configuration configuration) : base(ParadigmName) => Config = configuration;

        public override void Run(Session session) => new SsvepExperimentWindow(session).Show();

        [JsonIgnore]
        protected override IStageProvider[] StageProviders => new IStageProvider[]
        {
            new DelayStageProvider("Preparing...", 1000),
            new DelayStageProvider(Config.Test.Patterns.Join("\n"), 2500),
            new CountdownStageProvider(SystemVariables.PreparationCountdown.Get(SystemVariables.Context)),
            new ConditionStageProvider(Config.Test.Baseline.IsAvailable, new BaselineStageProvider(Config.Test.Baseline.Duration)),
            new MarkedStageProvider(MarkerDefinitions.ParadigmStartMarker),
            new DelayStageProvider(1000),
            new SsvepStageProvider(Config.Test),
            new MarkedStageProvider(MarkerDefinitions.ParadigmEndMarker),
            new ConditionStageProvider(Config.Test.Baseline.IsAvailable && Config.Test.Baseline.TwoSided, new BaselineStageProvider(Config.Test.Baseline.Duration)),
            new DelayStageProvider(3000)
        };

    }

}
