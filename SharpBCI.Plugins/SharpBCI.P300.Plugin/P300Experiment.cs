using System;
using System.Collections.Generic;
using System.Drawing;
using MarukoLib.Lang;
using MarukoLib.UI;
using Newtonsoft.Json;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Experiments;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Experiments.P300
{

    public static class P300ExperimentExt
    {

        public static uint[] GetLayoutSize(this P300Experiment.Configuration.TestConfig.BlockLayout layout)
        {
            switch (layout)
            {
                case P300Experiment.Configuration.TestConfig.BlockLayout.OneBlock:
                    return new uint[] { 1, 1 };
                case P300Experiment.Configuration.TestConfig.BlockLayout.ThreeBlocks:
                    return new uint[] { 1, 3 };
                case P300Experiment.Configuration.TestConfig.BlockLayout.NineBlocks:
                    return new uint[] { 3, 3 };
                case P300Experiment.Configuration.TestConfig.BlockLayout.TwentyFiveBlocks:
                    return new uint[] { 5, 5 };
                default:
                    throw new NotSupportedException();
            }
        }

    }

    [Experiment(ExperimentName, "1.0")]
    public class P300Experiment : StagedExperiment.Basic
    {

        public const string ExperimentName = "P300";

        public class Configuration
        {

            public class GuiConfig
            {

                public uint BackgroundColor;

                public RectangleLayout BlockLayout;

                public Position2D BlockPosition;

                public Border BlockBorder;

                public uint BlockNormalColor;

                public uint BlockActivedColor;

                public uint BlockTargetFlashingColor;

                public bool UseBitmap;

            }

            public class TestConfig
            {

                public enum P300Paradigm
                {
                    SingleFlash = 1,
                    DoubleFlash = 2
                }

                public enum BlockLayout
                {
                    OneBlock = 1,
                    ThreeBlocks = 3,
                    NineBlocks = 9,
                    TwentyFiveBlocks = 25
                }

                public BlockLayout Layout;

                public P300Paradigm Paradigm;

                public RandomTargetRate TargetRate;

                public uint TrialCount;

                public ulong TrialDuration;

                public ulong TrialInterval;

                public uint SubTrialCount;

                public ulong DoubleFlashTargetDelay;

                [JsonIgnore]
                public ulong SubTrialDuration => TrialDuration / SubTrialCount;

            }

            public GuiConfig Gui;

            public TestConfig Test;

        }

        public class Result : Core.Experiment.Result
        {


            public class Trial
            {

                public class SubTrial
                {

                    public ulong Timestamp;

                    public bool[] Flags;

                }

                public ICollection<SubTrial> SubTrials;

                public ulong Timestamp;

            }

            public ICollection<Trial> Trials;

            public override IEnumerable<Item> Items => new[] { new Item("Title", "Result") };

        }

        public class Factory : ExperimentFactory<P300Experiment>
        {

            // Test Config

            private static readonly Parameter<Configuration.TestConfig.BlockLayout> Layout = Parameter<Configuration.TestConfig.BlockLayout>.OfEnum("Layout");

            private static readonly Parameter<Configuration.TestConfig.P300Paradigm> Paradigm = Parameter<Configuration.TestConfig.P300Paradigm>.OfEnum("Paradigm");

            private static readonly Parameter<RandomTargetRate> TargetRate = new Parameter<RandomTargetRate>("Target Rate", new RandomTargetRate(true, 0.08F));

            private static readonly Parameter<uint> TrialCount = new Parameter<uint>("Trial Count", 20);

            private static readonly Parameter<ulong> TrialDuration = new Parameter<ulong>("Trial Duration", unit: "ms", null, 5000);

            private static readonly Parameter<uint> SubTrialCount = new Parameter<uint>("Sub-Trial Count", 20);

            private static readonly Parameter<ulong> TrialInterval = new Parameter<ulong>("Trial Interval", unit: "ms", null, 200);

            private static readonly Parameter<ulong> DoubleFlashTargetDelay = new Parameter<ulong>("Target Delay", "ms", "Double Flash Paradigm Only", 100);

            // GUI

            private static readonly Parameter<Color> BackgroundColor = new Parameter<Color>("Background Color", Color.Black);

            private static readonly Parameter<RectangleLayout> BlockLayout = Parameter<RectangleLayout>.CreateBuilder("Block Layout")
                .SetDefaultValue(new RectangleLayout(300, 10))
                .SetMetadata(RectangleLayout.Factory.SquareProperty, false)
                .SetMetadata(RectangleLayout.Factory.UnifyMarginProperty, false)
                .Build();

            private static readonly Parameter<Position2D> BlockPosition = new Parameter<Position2D>("Block Position", Position2D.CenterMiddle);

            private static readonly Parameter<Border> BlockBorder = new Parameter<Border>("Block Border", new Border(1, Color.White));

            private static readonly Parameter<Color> BlockNormalColor = new Parameter<Color>("Block Normal Color", Color.Black);

            private static readonly Parameter<Color> BlockActivedColor = new Parameter<Color>("Block Flashing Color", Color.White);

            private static readonly Parameter<Color> BlockTargetFlashingColor = new Parameter<Color>("Block Target Flashing Color", Color.Red);

            private static readonly Parameter<bool> UseBitmap = new Parameter<bool>("Use Bitmap", false);

            public override IReadOnlyCollection<ParameterGroup> ParameterGroups => new ParameterGroupCollection()
                .Add("Layout", Layout)
                .Add("Paradigm", Paradigm)
                .Add("Exp. Params", TargetRate, TrialCount, TrialDuration, SubTrialCount, TrialInterval, DoubleFlashTargetDelay)
                .Add("UI Basic", BackgroundColor)
                .Add("UI Block", BlockLayout, BlockPosition, BlockBorder, BlockNormalColor, BlockActivedColor, BlockTargetFlashingColor, UseBitmap);

            public override IReadOnlyCollection<ISummary> Summaries => new ISummary[]
            {
                new ComputationalSummary("Sub-Trial Duration", (context, exp) => ((P300Experiment)exp).Config.Test.SubTrialDuration + "ms"), 
            };
            
            public override P300Experiment Create(IReadonlyContext context) => new P300Experiment(new Configuration
            {
                Gui = new Configuration.GuiConfig
                {
                    BackgroundColor = BackgroundColor.Get(context, ColorUtils.ToUIntArgb),
                    BlockLayout = BlockLayout.Get(context),
                    BlockPosition = BlockPosition.Get(context),
                    BlockBorder = BlockBorder.Get(context),
                    BlockNormalColor = BlockNormalColor.Get(context, ColorUtils.ToUIntArgb),
                    BlockActivedColor = BlockActivedColor.Get(context, ColorUtils.ToUIntArgb),
                    BlockTargetFlashingColor = BlockTargetFlashingColor.Get(context, ColorUtils.ToUIntArgb),
                    UseBitmap = UseBitmap.Get(context),
                },
                Test = new Configuration.TestConfig
                {
                    Layout = Layout.Get(context),
                    Paradigm = Paradigm.Get(context),
                    TargetRate = TargetRate.Get(context),
                    TrialCount = TrialCount.Get(context),
                    TrialDuration = TrialDuration.Get(context),
                    TrialInterval = TrialInterval.Get(context),
                    SubTrialCount = SubTrialCount.Get(context),
                    DoubleFlashTargetDelay = DoubleFlashTargetDelay.Get(context),
                }
            });

        }

        private const string P300GroupName = "p300";

        [MarkerDefinition(P300GroupName)]
        public const int SubTrialMarker = MarkerDefinitions.CustomMarkerBase + 1;

        [MarkerDefinition(P300GroupName)]
        public const int OddBallEventMarker = MarkerDefinitions.CustomMarkerBase + 11;

        public readonly Configuration Config;

        public P300Experiment(Configuration configuration) : base(ExperimentName)
        {
            Config = configuration;
            // ReSharper disable once InvertIf
            if (configuration.Test.Paradigm == Configuration.TestConfig.P300Paradigm.DoubleFlash)
            {
                if (configuration.Test.SubTrialDuration == 0)
                    throw new ExperimentInitiationException("'sub-trial duration' be positive");
                //                if (configuration.Test.FlashingDuration > configuration.Test.DoubleFlashTargetDelay)
                //                    throw new ExperimentInitiationException("'Flashing duration' must shorter than 'Double flash target delay");
                //                if (configuration.Test.SubTrialDuration + configuration.Test.DoubleFlashTargetDelay > configuration.Test.TrialInterval)
                //                    throw new ExperimentInitiationException("ISI must longer than 2 * 'Flashing duration' + 'Double flash target delay");
            }
        }

        public override void Run(Session session) => new TestWindow(session).Show();

        [JsonIgnore]
        protected override IStageProvider[] StageProviders => new IStageProvider[]
        {
            new PreparationStageProvider(),
            new MarkedStageProvider(MarkerDefinitions.ExperimentStartMarker),
            new P300StageProvider(Config.Test),
            new MarkedStageProvider(MarkerDefinitions.ExperimentEndMarker),
            new DelayStageProvider(3000)
        };

    }

}
