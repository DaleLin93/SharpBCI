using JetBrains.Annotations;
using MarukoLib.Lang;
using Newtonsoft.Json;

namespace SharpBCI.Extensions.Data
{

    [ParameterizedObject(typeof(Factory))]
    public sealed class OptionalText : IParameterizedObject
    {

        public sealed class Factory : ParameterizedObjectFactory<OptionalText>
        {

            private static readonly Parameter<bool> Enabled = new Parameter<bool>("Enabled", false);

            private static readonly Parameter<string> Text = new Parameter<string>("Text", defaultValue: "");

            public override bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => !ReferenceEquals(parameter, Text) || Enabled.Get(context);

            public override OptionalText Create(IParameterDescriptor parameter, IReadonlyContext context) => new OptionalText(Enabled.Get(context), Text.Get(context));

            public override IReadonlyContext Parse(IParameterDescriptor parameter, OptionalText text) => new Context
            {
                [Enabled] = text.Enabled,
                [Text] = text.Text
            };

        }

        public readonly bool Enabled;

        [CanBeNull] public readonly string Text;

        public OptionalText() : this(false, null) { }

        public OptionalText([CanBeNull] string text) : this(text?.IsNotEmpty() ?? false, text) { }

        public OptionalText(bool enabled, [CanBeNull] string text)
        {
            Enabled = enabled;
            Text = text;
        }

        [JsonIgnore] public bool IsEmpty => Text?.IsEmpty() ?? true;

    }

}
