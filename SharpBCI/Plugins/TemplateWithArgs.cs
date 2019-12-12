using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;

namespace SharpBCI.Plugins
{

    public sealed class TemplateWithArgs<T> where T : TemplateAddOn
    {

        [NotNull] public readonly T Template;

        [NotNull] public readonly IReadonlyContext Args;

        public TemplateWithArgs([NotNull] T template) : this(template, EmptyContext.Instance) { }

        public TemplateWithArgs([NotNull] T template, IDictionary<string, string> serializedArgs)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Args = serializedArgs == null ? EmptyContext.Instance : (IReadonlyContext) template.DeserializeArgs(serializedArgs);
        }

        public TemplateWithArgs([NotNull] T template, [CanBeNull] IReadonlyContext args)
        {
            Template = template ?? throw new ArgumentNullException(nameof(template));
            Args = args ?? EmptyContext.Instance;
        }

        [CanBeNull]
        public static TemplateWithArgs<T> OfNullable([CanBeNull] T template, [CanBeNull] IReadonlyContext args) =>
            template == null ? null : new TemplateWithArgs<T>(template, args);

        [CanBeNull]
        public static TemplateWithArgs<T> OfNullable([CanBeNull] T template, [CanBeNull] IDictionary<string, string> serializedArgs) =>
            template == null ? null : new TemplateWithArgs<T>(template, serializedArgs);

        public TemplateWithArgs<T> ReplaceArgs([CanBeNull] IDictionary<string, string> serializedArgs) => new TemplateWithArgs<T>(Template, serializedArgs);

        public TemplateWithArgs<T> ReplaceArgs([CanBeNull] IReadonlyContext args) => new TemplateWithArgs<T>(Template, args);

        public SerializedObject Serialize() => new SerializedObject(Template.Identifier, Template.SerializeArgs(Args));

    }

}
