using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Paradigms;
using SharpBCI.Extensions.Presenters;
using Rectangle = SharpBCI.Extensions.Data.Rectangle;

namespace SharpBCI.Paradigms.WebBrowser
{

    [Paradigm(ParadigmName, typeof(Factory), "EEG", "1.0")]
    public class WebBrowserAssistantParadigm : Paradigm
    {

        public const string ParadigmName = "Web Browser Assistant [Server]";

        public class StimulationScheme 
        {

            private const int HighCutoff = 90;

            public StimulationScheme(IReadOnlyList<float> frequencies, IEnumerable<float> filterBankLowCutoffs, uint harmonicCount)
            {
                Frequencies = frequencies;
                FilterBank = filterBankLowCutoffs.Select(lowCutoff => new IdealBandpassFilterParams(lowCutoff, HighCutoff)).ToArray();
                HarmonicCount = harmonicCount;
            }

            public IReadOnlyList<float> Frequencies { get; }

            public IReadOnlyList<IdealBandpassFilterParams> FilterBank { get; }

            public uint HarmonicCount { get; }
        }

        public struct StimulationDuration : IAutoParameterizedObject
        {
            
            [AutoParam(Unit = "ms")]
            public uint Navigating;

            [AutoParam(Unit = "ms")]
            public uint Spelling;

            public StimulationDuration(uint navigating, uint spelling)
            {
                Navigating = navigating;
                Spelling = spelling;
            }

        }

        public class Configuration
        {

            public class SystemConfig
            {

                public bool DebugInformation;

                public ushort ListeningPort;

                public ArrayQuery<int> Channels;

            }

            public class UserConfig
            {

                public string HomePage;

                public string WebRootDir;

                public StimulationScheme StimulationScheme;

                public uint DwellSelectionDelay;

                public uint ConfirmationDelay;

                public bool EnableEdgeScrolling;

                public Rectangle EdgeScrollingHotAreaSize;

                public double EdgeScrollingSpeed;

                public uint CursorMovementTolerance;

                public StimulationDuration StimulationDuration;

                public bool CancelByMovement;

                public Rectangle StimulationSize;

            }

            public SystemConfig System;

            public UserConfig User;

        }

        public class Factory : ParadigmFactory<WebBrowserAssistantParadigm>
        {

            // Test

            private static readonly Parameter<bool> DebugInformation = new Parameter<bool>("Debug Information", false);

            private static readonly Parameter<Uri> HomePage = Parameter<Uri>.CreateBuilder("Home Page")
                .SetSelectableValues(new[] {new Uri("http://www.google.com/"), new Uri("http://www.baidu.com/"), new Uri("http://www.magi.com/"),}, true)
                .SetValidator(uri => uri.Scheme.Equals("http") || uri.Scheme.Equals("https"))
                .SetMetadata(Presenters.PresentTypeConverterProperty, TypeConverters.String2AbsoluteUri.Inverse())
                .SetMetadata(TypeConvertedPresenter.ConvertedContextProperty, new ContextBuilder()
                    .SetProperty(SelectablePresenter.CustomizableProperty, true)
                    .Build())
                .Build();

            private static readonly Parameter<Path> WebRootDir = Parameter<Path>.CreateBuilder("Web-root Dir")
                .SetDefaultValue(new Path(".\\"))
                .SetMetadata(PathPresenter.PathTypeProperty, PathPresenter.PathType.Directory)
                .SetMetadata(PathPresenter.CheckExistenceProperty, true)
                .Build();

            /// <summary>
            /// Low Frequency
            ///   H1: 7.0   7.5   8.0   8.5
            ///   H2: 14    15    16    17
            ///   H3: 21.0  22.5  24.0  25.5 
            ///   H4: 28    30    32    34
            ///   H5: 35.0  37.5  40.0  42.5  [X]
            /// 
            /// Filter bank design: [4] 7~8.5  [11.25]  14~17  [19]  21~25.5  [26.75]  28~34  [x]  35~42.5 {90}
            /// 
            /// Medium Frequency:
            ///   H1: 13 14 15 16
            ///   H2: 26 28 30 32
            ///   H3: 39 42 45 48
            ///   H4: 52 56 60 64
            ///   H5: 65 70 75 80  [X]
            /// 
            /// Filter bank design: [10] 13~16  [22]  26~32  [36]  39~48  [50]  52~64  [x]  65~80 {90}
            /// </summary>
            // ReSharper disable once MemberHidesStaticFromOuterClass
            private static readonly Parameter<StimulationScheme> StimulationScheme = Parameter<StimulationScheme>.CreateBuilder("Stimulation Scheme")
                .SetKeyedSelectableValues(new Dictionary<string, StimulationScheme>
                {
                    ["Low Frequency"] = new StimulationScheme(new[] {7, 7.5F, 8, 8.5F}, new[] {4, 11.25F, 19, 26.75F}, 4),
                    ["High Frequency"] = new StimulationScheme(new float[] {13, 14, 15, 16}, new float[] {10, 22, 36, 50}, 4),
                }, true)
                .Build();

            private static readonly Parameter<uint> DwellSelectionDelay = new Parameter<uint>("Dwell Selection Delay", "ms", null, 1000);

            private static readonly Parameter<uint> ConfirmationDelay = new Parameter<uint>("Confirmation Delay", "ms", null, 600);

            private static readonly Parameter<bool> EnableEdgeScrolling = new Parameter<bool>("Enable Edge Scrolling", true);

            private static readonly Parameter<Rectangle> EdgeScrollingHotAreaSize = Parameter<Rectangle>.CreateBuilderWithKey("edgeScrollingHotAreaSize", "Hot Area Size", new Rectangle(0.5, 0.2))
                .SetDescription("Range of width: (0, 1]; Range of height: (0, 0.5).")
                .SetValidator(rect => rect.Width > 0 && rect.Width <= 1 && rect.Height > 0 && rect.Height < 0.5)
                .SetMetadata(Rectangle.Factory.UnitProperty, "%")
                .Build();

            private static readonly Parameter<double> EdgeScrollingSpeed = Parameter<double>.CreateBuilderWithKey("edgeScrollingSpeed", "Speed", 0.2)
                .SetDescription("Range of speed: (0, 1].")
                .SetValidator(speed => speed > 0 && speed <= 1)
                .Build();

            private static readonly Parameter<uint> CursorMovementTolerance = new Parameter<uint>("Cursor Movement Tolerance", "dp", null, 300);

            private static readonly Parameter<ArrayQuery<int>> Channels = Parameter<ArrayQuery<int>>.CreateBuilder("Channels")
                .SetDescription("Channel indices in range of [1, channel num]")
                .SetDefaultQuery(":", TypeConverters.Double2Int)
                .Build();

            // ReSharper disable once MemberHidesStaticFromOuterClass
            private static readonly Parameter<StimulationDuration> StimulationDuration = new Parameter<StimulationDuration>("Stimulation Duration", new StimulationDuration(4000, 2000));

            private static readonly Parameter<bool> CancelByMovement = new Parameter<bool>("Cancel by Movement", true);

            private static readonly Parameter<Rectangle> StimulationSize = new Parameter<Rectangle>("Stimulation Size", new Rectangle(56, 19));

            // Communication

            private static readonly Parameter<ushort> ListeningPort = new Parameter<ushort>("Listening Port", description: null, defaultValue: 12315);

            // Group

            private static readonly ParameterGroup EdgeScrollingGroup = new ParameterGroup("Edge Scrolling", EdgeScrollingHotAreaSize, EdgeScrollingSpeed);

            public override IReadOnlyCollection<IGroupDescriptor> ParameterGroups => new ParameterGroupCollection()
                .Add("System", DebugInformation, ListeningPort, Channels)
                .Add("Web Browser", HomePage, WebRootDir)
                .Add("Eye-tracking Interaction", DwellSelectionDelay, CursorMovementTolerance, EnableEdgeScrolling, EdgeScrollingGroup, CancelByMovement)
                .Add("User", StimulationScheme, StimulationSize, StimulationDuration)
                .Add("Feedback", ConfirmationDelay);

            public override WebBrowserAssistantParadigm Create(IReadonlyContext context) => new WebBrowserAssistantParadigm(new Configuration
            {
                System = new Configuration.SystemConfig
                {
                    DebugInformation = DebugInformation.Get(context),
                    ListeningPort = ListeningPort.Get(context),
                    Channels = Channels.Get(context),
                },
                User = new Configuration.UserConfig
                {
                    HomePage = HomePage.Get(context).ToString(),
                    WebRootDir = WebRootDir.Get(context).Value,
                    StimulationScheme = StimulationScheme.Get(context),
                    DwellSelectionDelay = DwellSelectionDelay.Get(context),
                    ConfirmationDelay = ConfirmationDelay.Get(context),
                    EnableEdgeScrolling = EnableEdgeScrolling.Get(context),
                    EdgeScrollingHotAreaSize = EdgeScrollingHotAreaSize.Get(context),
                    EdgeScrollingSpeed = EdgeScrollingSpeed.Get(context),
                    CursorMovementTolerance = CursorMovementTolerance.Get(context),
                    StimulationDuration = StimulationDuration.Get(context),
                    CancelByMovement = CancelByMovement.Get(context),
                    StimulationSize = StimulationSize.Get(context)
                }
            });

            public override bool IsVisible(IReadonlyContext context, IDescriptor descriptor) => 
                ReferenceEquals(EdgeScrollingGroup, descriptor) ? EnableEdgeScrolling.Get(context) : base.IsVisible(context, descriptor);

        }

        private const string WebBrowserAssistantGroupName = "webbrowser";

        [Marker(WebBrowserAssistantGroupName)]
        public const int NormalModeOnSetMarker = MarkerDefinitions.CustomMarkerBase + (byte) Mode.Normal;

        [Marker(WebBrowserAssistantGroupName)]
        public const int ReadingModeOnSetMarker = MarkerDefinitions.CustomMarkerBase + (byte)Mode.Reading;

        private static readonly Icon BrowserIcon;

        public readonly Configuration Config;

        static WebBrowserAssistantParadigm()
        {
            var type = typeof(WebBrowserAssistantParadigm);
            var assembly = type.Assembly;
            using (var stream = assembly.GetManifestResourceStream($"{type.Namespace}.browser.ico") ?? throw new Exception()) BrowserIcon = new Icon(stream);
        }

        public WebBrowserAssistantParadigm(Configuration configuration) : base(ParadigmName) => Config = configuration;

        // ReSharper disable LocalizableElement
        public override void Run(Session session)
        {
            /* Initialization */
            var engine = new WebBrowserAssistantEngine(session);
            var notifyIcon = new NotifyIcon {Visible = true, Text = "Web Browser Assistant Server", Icon = BrowserIcon };
            var notifyContextMenu = new ContextMenuStrip();
            var modeMenuItem = new ToolStripMenuItem();
            modeMenuItem.Click += (sender, e) => engine.SwitchMode();
            notifyContextMenu.Items.Add(modeMenuItem);
            notifyContextMenu.Items.Add(new ToolStripSeparator());
            var stopMenuItem = new ToolStripMenuItem {Text = "Stop"};
            stopMenuItem.Click += (sender, e) => engine.Stop();
            notifyContextMenu.Items.Add(stopMenuItem);
            notifyContextMenu.Opening += delegate { modeMenuItem.Text = $"Current Mode: {engine.Mode.ToString()}"; };
            notifyIcon.ContextMenuStrip = notifyContextMenu;

            /* Start system */
            session.Start();
            engine.Start();
            notifyIcon.ShowBalloonTip(1, "Web Browser Assistant Started", "Right click on tray icon to stop.", ToolTipIcon.Info);

            /* Wait until system stopped by user */
            while (!engine.WaitForStop(TimeSpan.FromMilliseconds(200))) Application.DoEvents();

            /* Release resources */
            session.Finish(null, false);
            notifyIcon.Dispose();
        }

    }

}
