using System;
using System.Collections.Generic;
using System.Drawing;
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

                public Uri HomePage;

                public string WebRootDir;

                public uint DwellSelectionDelay;

                public uint ConfirmationDelay;

                public double EdgeScrollRatio;

                public uint CursorMovementTolerance;

                public uint NavigatingTrialDuration;

                public uint SpellingTrialDuration;

                public bool TrialCancellable;

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
                .SetDefaultValue(new Uri("http://www.google.com/"))
                .SetMetadata(UriPresenter.SupportedSchemesProperty, new[] {"https", "http"})
                .Build();

            private static readonly Parameter<Path> WebRootDir = Parameter<Path>.CreateBuilder("Web Root Directory")
                .SetDefaultValue(new Path(".\\"))
                .SetMetadata(PathPresenter.PathTypeProperty, PathPresenter.PathType.Directory)
                .SetMetadata(PathPresenter.CheckExistenceProperty, true)
                .Build();

            private static readonly Parameter<uint> DwellSelectionDelay = new Parameter<uint>("Dwell Selection Delay", "ms", null, 1000);

            private static readonly Parameter<uint> ConfirmationDelay = new Parameter<uint>("Confirmation Delay", "ms", null, 600);

            private static readonly Parameter<Optional<double>> EdgeScrollRatio = Parameter<Optional<double>>.CreateBuilder("Edge Scroll Ratio", new Optional<double>(0.2))
                .SetDescription("Valid range of value: (0, 0.5)")
                .SetValidator(value => !value.HasValue || value.Value > 0 && value.Value < 0.5)
                .Build();

            private static readonly Parameter<uint> CursorMovementTolerance = new Parameter<uint>("Cursor Movement Tolerance", "dp", null, 300);

            private static readonly Parameter<ArrayQuery<int>> Channels = Parameter<ArrayQuery<int>>.CreateBuilder("Channels")
                .SetDescription("Channel indices in range of [1, channel num]")
                .SetDefaultQuery(":", TypeConverters.Double2Int)
                .Build();

            private static readonly Parameter<ComplexObject> TrialDuration = Parameter<ComplexObject>.CreateBuilder("Trial Duration")
                .SetMetadata(ComplexObject.Factory.FieldsProperty, new IParameterDescriptor[] {new Parameter<uint>("Navigating", 4000), new Parameter<uint>("Spelling", 2000)})
                .Build();

            private static readonly Parameter<bool> TrialCancellable = new Parameter<bool>("Trial Cancellable", true);

            private static readonly Parameter<Rectangle> StimulationSize = new Parameter<Rectangle>("Stimulation Size", new Rectangle(56, 19));

            // Communication

            private static readonly Parameter<ushort> ListeningPort = new Parameter<ushort>("Listening Port", description: null, defaultValue: 12315);

            public override IReadOnlyCollection<IGroupDescriptor> ParameterGroups => new ParameterGroupCollection()
                .Add("System", DebugInformation, ListeningPort, Channels)
                .Add("User", HomePage, WebRootDir, DwellSelectionDelay, ConfirmationDelay, EdgeScrollRatio, CursorMovementTolerance, TrialDuration, TrialCancellable, StimulationSize);

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
                    HomePage = HomePage.Get(context),
                    WebRootDir = WebRootDir.Get(context).Value,
                    DwellSelectionDelay = DwellSelectionDelay.Get(context),
                    ConfirmationDelay = ConfirmationDelay.Get(context),
                    EdgeScrollRatio = EdgeScrollRatio.Get(context, op => op.HasValue ? op.Value : double.NaN),
                    CursorMovementTolerance = CursorMovementTolerance.Get(context),
                    NavigatingTrialDuration = (uint)TrialDuration.Get(context)[0],
                    SpellingTrialDuration = (uint)TrialDuration.Get(context)[1],
                    TrialCancellable = TrialCancellable.Get(context),
                    StimulationSize = StimulationSize.Get(context)
                }
            });

        }

        private const string WebBrowserAssistantGroupName = "webbrowser";

        [MarkerDefinition(WebBrowserAssistantGroupName)]
        public const int NormalModeOnSetMarker = MarkerDefinitions.CustomMarkerBase + (byte) Mode.Normal;

        [MarkerDefinition(WebBrowserAssistantGroupName)]
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
