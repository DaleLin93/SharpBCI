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
using SharpBCI.Extensions.Paradigms;
using SharpBCI.Extensions.StageProviders;

namespace SharpBCI.Paradigms.MI
{

    [Paradigm(ParadigmName, typeof(Factory), "EEG", "1.0", Description = "A stimulation client for motor imagery paradigm.")]
    public class MiParadigm : Paradigm
    {

        public const string ParadigmName = "Motor Imaginary (MI) [Client]";

        public class Configuration
        {

            public class GuiConfig
            {

                public uint BackgroundColor;

                public uint FontSize;

                public uint FontColor;

                public Colors ProgressBarColor;

                public Border ProgressBarBorder;

            }

            public class CommConfig
            {

                public string ServerAddress;

            }

            public class ExperimentalConfig
            {

#if DEBUG
                public bool UseInternalProgram;
#endif

                public bool Repeat;

                public bool ForceReset;

                /// <summary>
                /// Gaze to focus duration in milliseconds.
                /// </summary>
                public uint GazeToFocusDuration;

                public string PreLoadResourceListFile;

            }

            public GuiConfig Gui;

            public CommConfig Comm;

            public ExperimentalConfig Experimental;

        }

        public class Result : Core.Experiment.Result
        {

            public struct DebugLog
            {

                [JsonProperty(nameof(Timestamp))] public readonly DateTime Timestamp;

                [JsonProperty(nameof(Tag))] public readonly string Tag;

                [JsonProperty(nameof(Value))] public readonly object Value;

                [JsonConstructor]
                public DebugLog(
                    [JsonProperty(nameof(Timestamp))] DateTime timestamp, 
                    [JsonProperty(nameof(Tag))]  string tag, 
                    [JsonProperty(nameof(Value))] object value)
                {
                    Timestamp = timestamp;
                    Tag = tag;
                    Value = value;
                }

                public static DebugLog Of(string tag, object value) => new DebugLog(DateTime.Now, tag, value);

            }

            public ulong Duration; // milliseconds

#if DEBUG
            public IReadOnlyCollection<DebugLog> Logs;
#endif

            public override IEnumerable<Item> Items => new[] { new Item("Duration", $"{TimeSpan.FromMilliseconds(Duration).TotalMinutes:G2} min") };

        }

        public class Factory : ParadigmFactory<MiParadigm>
        {

            // Experimental

#if DEBUG
            private static readonly Parameter<bool> UseInternalProgram = new Parameter<bool>("Use Internal Program", false);
#endif

            private static readonly Parameter<bool> Repeat = new Parameter<bool>("Repeat", false);

            private static readonly Parameter<bool> ForceReset = new Parameter<bool>("Force Reset", false);

            private static readonly Parameter<float> GazeToFocusDuration = Parameter<float>.CreateBuilder("Gaze To Focus Duration", 2)
                .SetUnit("s").SetValidator(Predicates.Nonnegative).Build();

            private static readonly Parameter<Path> PreLoadResourceList = Parameter<Path>.CreateBuilder("Pre-load Resource List").SetNullable(true).Build();

            // Communication

            private static readonly Parameter<string> ServerAddress = new Parameter<string>("Server Address", description: null, defaultValue: "127.0.0.1:4567");

            // GUI

            private static readonly Parameter<Color> BackgroundColor = new Parameter<Color>("Background Color", Color.Black);

            private static readonly Parameter<uint> FontSize = new Parameter<uint>("Font Size", 90);

            private static readonly Parameter<Color> FontColor = new Parameter<Color>("Font Color", Color.Red);

            private static readonly Parameter<Colors> ProgressBarColor = Parameter<Colors>.CreateBuilder("Progress Bar Color", new Colors(0xFFCCCCCC, 0xFFCC2222))
                .SetMetadata(ParameterizedObjectExt.PopupProperty, true)
                .Build();

            private static readonly Parameter<Border> ProgressBarBorder = new Parameter<Border>("Progress Bar Border", new Border(0, Color.White));

            public override IReadOnlyCollection<IGroupDescriptor> ParameterGroups => new[]
            {
#if DEBUG
                new ParameterGroup("Diagnostics", UseInternalProgram),
#endif
                new ParameterGroup("Settings", Repeat, ForceReset, GazeToFocusDuration, PreLoadResourceList),
                new ParameterGroup("Communication", ServerAddress),
                new ParameterGroup("UI Window", BackgroundColor),
                new ParameterGroup("UI Font", FontSize, FontColor),
                new ParameterGroup("UI Progress Bar", ProgressBarColor, ProgressBarBorder),
            };

            public override MiParadigm Create(IReadonlyContext context) => new MiParadigm(new Configuration
            {
                Gui = new Configuration.GuiConfig
                {
                    BackgroundColor = BackgroundColor.Get(context, ColorUtils.ToUIntArgb),
                    FontSize = FontSize.Get(context),
                    FontColor = FontColor.Get(context, ColorUtils.ToUIntArgb),
                    ProgressBarColor = ProgressBarColor.Get(context),
                    ProgressBarBorder = ProgressBarBorder.Get(context)
                },
                Experimental = new Configuration.ExperimentalConfig
                {
#if DEBUG
                    UseInternalProgram = UseInternalProgram.Get(context),
#endif
                    Repeat = Repeat.Get(context),
                    ForceReset = ForceReset.Get(context),
                    GazeToFocusDuration = (uint) (GazeToFocusDuration.Get(context) * 1000),
                    PreLoadResourceListFile = PreLoadResourceList.Get(context)?.Value,
                },
                Comm = new Configuration.CommConfig
                {
                    ServerAddress = ServerAddress.Get(context),
                }
            });

        }

        public readonly Configuration Config;

        public MiParadigm(Configuration configuration) : base(ParadigmName) => Config = configuration;

#if DEBUG
        private static IStageProvider[] InternalProgramStageProviders => new IStageProvider[]
        {
            new DelayStageProvider(100),
            new MarkedStageProvider(MarkerDefinitions.ParadigmStartMarker),
            new RepeatingStageProvider.Simple(new Stage[]
            {
                new MiStage {VisualStimulus = MiStage.Stimulus<MiStage.VisualStimulusType>.Parse("image:file://F:/mi materials/elbow_up_4s.gif"), Duration = 3000, IsPreload = false},
                new MiStage {Duration = 3000, IsPreload = false}
            }, 50), 
            new MarkedStageProvider(MarkerDefinitions.ParadigmEndMarker),
            new DelayStageProvider(1000)
        };
#endif

        public override void Run(Session session) => new MiExperimentWindow(session).ShowDialog();

        internal StageProgram CreateStagedProgram(Session session, MiStimClient client) =>
#if DEBUG
            Config.Experimental.UseInternalProgram ? new StageProgram(session.Clock, InternalProgramStageProviders) : 
#endif
                new StageProgram(session.Clock, new MiRemoteStageProvider(client));

    }

}
