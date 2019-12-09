using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Persistence;

namespace SharpBCI.Extensions
{

    public static class SystemVariables
    {

        public static readonly Parameter<uint> PreparationCountdown = new Parameter<uint>("Preparation Countdown", "sec", null, 10U);

        public static readonly Parameter<bool> DisableUiAnimation = new Parameter<bool>("Disable UI Animation", false);

        public static readonly IDescriptor[] ParameterDefinitions =
        {
            new ParameterGroup("Experiment", PreparationCountdown),
            new ParameterGroup("GUI", DisableUiAnimation),
        };

        public static readonly TransactionalContext Context = new TransactionalContext();

        private static readonly IParameterDescriptor[] AllParameters = ParameterDefinitions.GetAllParameters().ToArray();

        public static void Apply([CanBeNull] IReadonlyContext context)
        {
            if (context == null) return;
            var transaction = Context.CreateTransaction();
            foreach (var property in context.Properties)
                transaction[property] = context[property];
            transaction.Commit();
        }

        public static void Serialize([NotNull] string filePath) => Serialize().JsonSerializeToFile(filePath, JsonUtils.PrettyFormat);

        [NotNull]
        public static IDictionary<string, string> Serialize() => AllParameters.SerializeArgs(Context);

        public static void Deserialize([NotNull] string filePath) => Deserialize(JsonUtils.DeserializeFromFile<IDictionary<string, string>>(filePath));

        public static void Deserialize([CanBeNull] IDictionary<string, string> input)
        {
            if (input == null) return;
            Apply(AllParameters.DeserializeArgs(input));
        }

    }

}
