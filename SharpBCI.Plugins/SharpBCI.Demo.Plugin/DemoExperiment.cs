using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Experiments;
using System.Collections.Generic;
using System.Drawing;

namespace SharpBCI.Experiments.Demo
{

    [Experiment(ExperimentName, "1.0", Description = "This is a simple demo.")]
    public class DemoExperiment : Experiment
    {

        public const string ExperimentName = "Demo";

        /// <summary>
        /// The factory class of demo experiment.
        /// </summary>
        public class Factory : ExperimentFactory<DemoExperiment>
        {

            /// <summary>
            /// Text parameter 
            /// </summary>
            private static readonly Parameter<string> Text = new Parameter<string>("Text", null, description: "The text to display", defaultValue: "Demo");

            private static readonly Parameter<Color> ForegroundColor = new Parameter<Color>("Foreground Color", null, "The color of text", Color.Red);

            private static readonly Parameter<Color> BackgroundColor = new Parameter<Color>("Background Color", null, "The background color of window", Color.Black);

            public override IReadOnlyCollection<ParameterGroup> ParameterGroups => new[] { new ParameterGroup(Text, ForegroundColor, BackgroundColor), };

            /// <summary>
            /// Create the demo experiment with given context.
            /// </summary>
            /// <param name="context">The context that containing required parameters.</param>
            /// <returns>The created experiment instance.</returns>
            public override DemoExperiment Create(IReadonlyContext context)
            {
                var experiment = new DemoExperiment();
                experiment.Text = Text.Get(context);
                experiment.ForegroundColor = ForegroundColor.Get(context, ColorUtils.ToUIntArgb);
                experiment.BackgroundColor = BackgroundColor.Get(context, ColorUtils.ToUIntArgb);
                return experiment;
            }

        }

        /// <summary>
        /// Text to display in the window.
        /// </summary>
        public string Text;

        /// <summary>
        /// The color of text.
        /// </summary>
        public uint ForegroundColor;

        /// <summary>
        /// The background color of window.
        /// </summary>
        public uint BackgroundColor;

        internal DemoExperiment() : base(ExperimentName) { }

        public override void Run(Session session) => new TestWindow(session, this).ShowDialog();

    }

}
