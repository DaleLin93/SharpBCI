using System.Drawing;
using System.Reflection;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions;
using SharpBCI.Extensions.Paradigms;

namespace SharpBCI.Paradigms.Demo
{

    [Paradigm(ParadigmName, typeof(AutoParadigmFactory), "1.0", Description = "This is a simple demo.")]
    public class DemoParadigm : Paradigm
    {

        public const string ParadigmName = "Demo";

        /// <summary>
        /// Implement 'IAutoParamAdapter' to provide value validation of parameter.
        /// </summary>
        private class ParamAdapter : IAutoParamAdapter
        {

            public bool IsValid(FieldInfo field, object value)
            {
                if (field.Name == nameof(FontSize)) return value is ushort val && val > 0;
                return true;
            }

        }

        /// <summary>
        /// The text to display in the window.
        /// </summary>
        [AutoParam]
        public string Text = "Demo";

        /// <summary>
        /// Font size of the text to display in the window.
        /// </summary>
        [AutoParam("Font Size", Unit = "dp", Desc = "Font size of the text to display in the window.", AdapterType = typeof(ParamAdapter))]
        public ushort FontSize = 80;

        /// <summary>
        /// The background color of window.
        /// </summary>
        [AutoParam("Background Color", Desc = "The background color of the window.")]
        public Color BackgroundColor = Color.Black;

        /// <summary>
        /// The color of the text.
        /// </summary>
        [AutoParam("Foreground Color", Desc = "The color of the text.")]
        public Color ForegroundColor = Color.Red;

        public DemoParadigm() : base(ParadigmName) { }

        public override void Run(Session session) => new DemoExperimentWindow(session, this).ShowDialog();

    }

}
