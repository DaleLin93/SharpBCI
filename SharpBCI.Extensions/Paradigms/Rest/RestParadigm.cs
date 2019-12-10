using System.Collections.Generic;
using System.Drawing;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Extensions.Paradigms.Rest
{

    [Paradigm(ParadigmName, typeof(Factory), "Basic", "1.0", Description = "A simple resting state paradigm with specific cue.")]
    public class RestParadigm : StagedParadigm.Basic
    {

        public const string ParadigmName = "Rest";

        public class Configuration
        {

            public class GuiConfig
            {

                public uint BackgroundColor;

                public uint ForegroundColor;

                public uint FontSize;

                public string Cue;

            }

            public class TestConfig
            {

                public uint Duration;

            }

            public GuiConfig Gui;

            public TestConfig Test;

        }

        public class Factory : ParadigmFactory<RestParadigm>
        {

            // Test

            private static readonly Parameter<uint> Duration = new Parameter<uint>("Duration", unit:"ms", null, defaultValue: 5000);

            // GUI

            private static readonly Parameter<Color> BackgroundColor = new Parameter<Color>("Background Color", Color.Black);

            private static readonly Parameter<Color> ForegroundColor = new Parameter<Color>("Foreground Color", Color.Red);

            private static readonly Parameter<uint> FontSize = new Parameter<uint>("Font Size", 90);

            private static readonly Parameter<string> Cue = new Parameter<string>("Display Content", defaultValue: "Resting");
            
            public override IReadOnlyCollection<IGroupDescriptor> ParameterGroups => new[] { new ParameterGroup(Cue, Duration, BackgroundColor,ForegroundColor) };

            public override RestParadigm Create(IReadonlyContext context) => new RestParadigm(new Configuration
            {
                Gui = new Configuration.GuiConfig
                {
                    BackgroundColor = BackgroundColor.Get(context, ColorUtils.ToUIntArgb),
                    ForegroundColor = ForegroundColor.Get(context, ColorUtils.ToUIntArgb),
                    FontSize = FontSize.Get(context),
                    Cue = Cue.Get(context)
                },
                Test = new Configuration.TestConfig
                {
                    Duration = Duration.Get(context),
                }
            });

        }

        public readonly Configuration Config;

        public RestParadigm(Configuration configuration) : base(ParadigmName) => Config = configuration;

        public override void Run(Session session) => new RestExperimentWindow(session).ShowDialog();

        protected override IStageProvider[] StageProviders => new IStageProvider[] { new DelayStageProvider(Config.Gui.Cue, Config.Test.Duration) };

    }

}
