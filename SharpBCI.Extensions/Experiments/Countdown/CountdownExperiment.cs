using System.Collections.Generic;
using System.Drawing;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Extensions.Experiments.Countdown
{

    [Experiment(ExperimentName, "1.0", Description = "A basic count down program.")]
    public class CountdownExperiment : StagedExperiment.Basic
    {

        public const string ExperimentName = "Countdown";

        public class Configuration
        {

            public class GuiConfig
            {

                public uint BackgroundColor;

                public uint ForegroundColor;

                public uint FontSize;

            }

            public class TestConfig
            {

                public uint Seconds;

            }

            public GuiConfig Gui;

            public TestConfig Test;

        }

        public class Factory : ExperimentFactory<CountdownExperiment>
        {

            // Test

            private static readonly Parameter<uint> Seconds = new Parameter<uint>("Seconds", unit:"s", null, 60);

            // GUI

            private static readonly Parameter<Color> BackgroundColor = new Parameter<Color>("Background Color", Color.Black);

            private static readonly Parameter<Color> ForegroundColor = new Parameter<Color>("Foreground Color", Color.Red);

            private static readonly Parameter<uint> FontSize = new Parameter<uint>("Font Size", 90);

            public override IReadOnlyCollection<ParameterGroup> ParameterGroups => ScanGroups(typeof(Factory));

            public override IReadOnlyCollection<ISummary> Summaries => ScanSummaries(typeof(Factory));

            public override CountdownExperiment Create(IReadonlyContext context) => new CountdownExperiment(new Configuration
            {
                Gui = new Configuration.GuiConfig
                {
                    BackgroundColor = BackgroundColor.Get(context, ColorUtils.ToUIntArgb),
                    ForegroundColor = ForegroundColor.Get(context, ColorUtils.ToUIntArgb),
                    FontSize = FontSize.Get(context),
                },
                Test = new Configuration.TestConfig
                {
                    Seconds = Seconds.Get(context),
                }
            });

        }

        public readonly Configuration Config;

        public CountdownExperiment(Configuration configuration) : base(ExperimentName) => Config = configuration;

        public override void Run(Session session) => new TestWindow(session).ShowDialog();

        protected override IStageProvider[] StageProviders => new IStageProvider[] { new CountdownStageProvider(Config.Test.Seconds) };

    }

}
