using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Text;
using MarukoLib.Lang;
using MarukoLib.UI;
using Newtonsoft.Json;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.Staging;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Paradigms;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.Speller.MidasTouch
{

    public enum MidasTouchControlStrategy
    {
        Gaze, Blink, SteadyStateVisuallyEvokedPotential, P300, MovementRelatedCorticalPotential, MotorImagery, Electromyogram
    }

    public static class MidasTouchParadigmExt
    {

        private static readonly IDictionary<string, MidasTouchControlStrategy> ControlParadigms = new Dictionary<string, MidasTouchControlStrategy>();

        static MidasTouchParadigmExt()
        {
            foreach (var value in Enum.GetValues(typeof(MidasTouchControlStrategy)))
            {
                var paradigm = (MidasTouchControlStrategy)value;
                ControlParadigms[paradigm.GetName()] = paradigm;
            }
        }

        public static readonly TypeConverter TypeConverter = TypeConverter.Of<MidasTouchControlStrategy, string>(p => p.GetName(), s => ControlParadigms[s]);

        public static string GetName(this MidasTouchControlStrategy controlStrategy)
        {
            var builder = new StringBuilder();
            foreach (var c in controlStrategy.ToString())
            {
                if (char.IsUpper(c) && builder.Length > 0)
                    builder.Append(' ');
                builder.Append(c);
            }
            return builder.ToString();
        }

    }

    [Paradigm(ParadigmName, typeof(Factory), "EEG", "1.0")]
    public class MidasTouchParadigm : StagedParadigm.Basic
    {

        public const string ParadigmName = "Midas Touch";

        public class Configuration
        {

            public class GuiConfig
            {

                public int Screen;

                public Colors ColorScheme;

                public uint ButtonSize;

                public Border ButtonBorder;

                public Margins ButtonPaddings;

                public uint ButtonNormalColor;

                public uint ButtonFlashingColor;

            }

            public class TestConfig
            {

                public ulong BaselineDuration;

                public ulong TrialDuration;

                public uint TrialCount;

                public ulong InterStimulusInterval;

                public RandomTargetRate TargetRate;

            }

            public GuiConfig Gui;

            public TestConfig Test;

        }

        public class Result : Core.Experiment.Result
        {

            public override IEnumerable<Item> Items => new[] { new Item("Title", "Result") };

        }

        [SuppressMessage("ReSharper", "UnusedMember.Local")]
        public class Factory : ParadigmFactory<MidasTouchParadigm>
        {

            #region Parameters

            // Test Config

            private static readonly Parameter<ulong> BaselineDuration = new Parameter<ulong>("Baseline Duration", unit: "ms", null, 10000);

            private static readonly Parameter<ulong> TrialDuration = new Parameter<ulong>("Trial Duration", unit: "ms", null, 20000);

            private static readonly Parameter<uint> TrialCount = new Parameter<uint>("Trial Count", null, null, Predicates.Positive, 10);

            private static readonly Parameter<ulong> InterStimulusInterval = new Parameter<ulong>("ISI", unit: "ms", description: "Inter-Stimulus Interval", 5000);

            private static readonly Parameter<RandomTargetRate> TargetRate = new Parameter<RandomTargetRate>("Target Rate", new RandomTargetRate(true, (decimal) 0.08));

            // GUI

            private static readonly Parameter<ScreenInfo> Screen = Parameter<ScreenInfo>.CreateBuilder("Screen").SetSelectableValues(ScreenInfo.All, true).Build();

            private static readonly Parameter<Colors> ColorScheme = new Parameter<Colors>("Color Scheme", new Colors(Color.Black, Color.White));

            private static readonly Parameter<uint> ButtonSize = new Parameter<uint>("Button Size", description: "0 - Automatically fill screen", 300);

            private static readonly Parameter<Border> ButtonBorder = new Parameter<Border>("Button Border", new Border(1, Color.White));

            private static readonly Parameter<Margins> ButtonPaddings = new Parameter<Margins>("Button Paddings", new Margins(true, 0.1));

            private static readonly Parameter<Color> ButtonNormalColor = new Parameter<Color>("Button Normal Color", Color.Black);

            private static readonly Parameter<Color> ButtonFlashingColor = new Parameter<Color>("Button Flashing Color", Color.White);

            #endregion

            #region Groups

            private static readonly ParameterGroup DisplayGroup = new ParameterGroup("Display", Screen);

            private static readonly ParameterGroup ParadigmParamsGroup = new ParameterGroup("Experimental", BaselineDuration, TrialDuration, TrialCount, InterStimulusInterval, TargetRate);

            private static readonly ParameterGroup UiBasicGroup = new ParameterGroup("UI Basic", ColorScheme);

            private static readonly ParameterGroup UiButtonGroup = new ParameterGroup("UI Button", ButtonSize, ButtonBorder, ButtonPaddings, ButtonNormalColor, ButtonFlashingColor);

            #endregion

            #region Summaries

            private static readonly ISummary ParadigmDurationSummary = new Summary("Est. Paradigm Duration", (context, paradigm) =>
                $"{((MidasTouchParadigm) paradigm).StageProviders.GetStages().GetDuration().TotalSeconds} s");

            #endregion
            
            public override IReadOnlyCollection<IGroupDescriptor> ParameterGroups => ScanGroups(typeof(Factory));

            public override IReadOnlyCollection<ISummary> Summaries => ScanSummaries(typeof(Factory));

            public override MidasTouchParadigm Create(IReadonlyContext context) => new MidasTouchParadigm(new Configuration
            {
                Gui = new Configuration.GuiConfig
                {
                    Screen = Screen.Get(context)?.Index ?? -1,
                    ColorScheme = ColorScheme.Get(context),
                    ButtonSize = ButtonSize.Get(context),
                    ButtonBorder = ButtonBorder.Get(context),
                    ButtonPaddings = ButtonPaddings.Get(context),
                    ButtonNormalColor = ButtonNormalColor.Get(context, ColorUtils.ToUIntArgb),
                    ButtonFlashingColor = ButtonFlashingColor.Get(context, ColorUtils.ToUIntArgb),
                },
                Test = new Configuration.TestConfig
                {
                    BaselineDuration = BaselineDuration.Get(context),
                    TrialDuration = TrialDuration.Get(context),
                    TrialCount = TrialCount.Get(context),
                    InterStimulusInterval = InterStimulusInterval.Get(context),
                    TargetRate = TargetRate.Get(context),
                }
            });

        }

        public readonly Configuration Config;

        public MidasTouchParadigm(Configuration configuration) : base(ParadigmName) => Config = configuration;

        public override void Run(Session session) => new MidasTouchWindow(session).Show();

        [JsonIgnore]
        protected override IStageProvider[] StageProviders => new IStageProvider[]
        {
            new PreparationStageProvider(),
            new BaselineStageProvider(Config.Test.BaselineDuration), 
            new MarkedStageProvider(MarkerDefinitions.ParadigmStartMarker),
            new DelayStageProvider(1000),
            new MidasTouchStageProvider(Config.Test), 
            new MarkedStageProvider(MarkerDefinitions.ParadigmEndMarker),
            new BaselineStageProvider(Config.Test.BaselineDuration),
            new DelayStageProvider(3000)
        };

    }

}
