using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Persistence;

namespace SharpBCI.Extensions
{

    public static class SystemVariables
    {

        public static readonly Parameter<uint> PreparationCountdown = new Parameter<uint>("Preparation Countdown", "sec", null, 10U);

        public static readonly IParameterDescriptor[] ParameterDefinitions = { PreparationCountdown };

        public static readonly TransactionalContext Context = new TransactionalContext();

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
        public static IDictionary<string, string> Serialize() => ParameterDefinitions.SerializeParams(Context);

        public static void Deserialize([NotNull] string filePath) => Deserialize(JsonUtils.DeserializeFromFile<IDictionary<string, string>>(filePath));

        public static void Deserialize([CanBeNull] IDictionary<string, string> input)
        {
            if (input == null) return;
            Apply(ParameterDefinitions.DeserializeParams(input));
        }

    }

}
