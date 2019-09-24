using JetBrains.Annotations;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.Logging;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions;
using SharpBCI.Plugins;

namespace SharpBCI
{

#pragma warning disable 0649
    public struct ParameterizedEntity
    {

        public string Id;

        public string Version;

        public IDictionary<string, string> Params;

        public ParameterizedEntity(string id, IDictionary<string, string> @params) : this(id, null, @params) { }

        public ParameterizedEntity(string id, string version, IDictionary<string, string> @params)
        {
            Id = id;
            Version = version;
            Params = @params;
        }

    }

    public struct DeviceParams
    {

        public ParameterizedEntity Device;

        public ParameterizedEntity[] Consumers;

    }

    public struct MultiSessionConfig
    {

        public const string FileSuffix = ".mscfg";

        public string Subject;

        public string[] ExperimentConfigs;

        public string DeviceConfig;

    }

    public struct SessionConfig
    {

        public struct Experiment
        {

            [SuppressMessage("ReSharper", "MemberHidesStaticFromOuterClass")]
            public const string FileSuffix = ".sexp";

            public string Subject;

            public string SessionDescriptor;

            public ParameterizedEntity Params;

        }

        public const string FileSuffix = ".scfg";

        public const string DeviceFileSuffix = ".scfg";

        public Experiment ExperimentPart;

        public IDictionary<string, DeviceParams> DevicePart;

        public bool Monitor;

    }
#pragma warning restore 0649

    public static class SessionConfigExt
    {

        private static readonly Logger Logger = Logger.GetLogger(typeof(SessionConfigExt));

        [UsedImplicitly]
        private class DslExpressionFactory
        {

            public sealed class DslExpression 
            {

                public DslExpression(string expression, Func<IDictionary<string, object>, string> evaluate)
                {
                    Expression = expression;
                    DelegatedFunction = evaluate;
                }

                public string Expression { get; }

                public Func<IDictionary<string, object>, string> DelegatedFunction { get; }

                public object Evaluate(IDictionary<string, object> context)
                {
                    try
                    {
                        return DelegatedFunction(context);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"failed to evaluate expression '{Expression}'", e);
                    }
                }

            }

            private static readonly Regex VariableRegex = new Regex("^[A-Za-z_][\\w]*$", RegexOptions.Compiled);

            private static readonly Regex VariableIndexerRegex = new Regex("^([A-Za-z_][\\w]*)\\[(\\d+)\\]$", RegexOptions.Compiled);

            private DslExpressionFactory() { }

            [NotNull]
            public static DslExpression Create([NotNull] string str)
            {
                string ToStringFunc(object obj)
                {
                    while (true)
                    {
                        if (!(obj is Array array)) return obj?.ToString();
                        if (array.Length > 1) return $"[{array.Cast<object>().Join(",", ToStringFunc)}]";
                        obj = array.GetValue(0);
                    }
                }

                str = str.Trim();
                if (VariableRegex.IsMatch(str))
                    return new DslExpression(str, dict => ToStringFunc(dict[str]));
                var match = VariableIndexerRegex.Match(str);
                if (!match.Success) throw new ArgumentException("unsupported expression: " + str);
                var variable = match.Groups[1].Value;
                var index = int.Parse(match.Groups[2].Value);
                return new DslExpression(str, dict => ToStringFunc(((Array)dict[variable]).GetValue(index)));
            }

        }

        public static string GetFullSessionName(this SessionConfig.Experiment experimentPart, long? time = null) => 
            Session.GetFullSessionName(time, experimentPart.Subject, GetFormattedSessionDescriptor(experimentPart));

        public static string GetFormattedSessionDescriptor(this SessionConfig.Experiment experimentPart)
        {
            var sessionName = experimentPart.SessionDescriptor;
            dynamic expandoContext = new ExpandoObject();
            if (experimentPart.Params.Params != null && App.Instance.Registries.Registry<PluginExperiment>().LookUp(experimentPart.Params.Id, out var exp))
            {
                var context = exp.DeserializeParams(experimentPart.Params.Params);
                foreach (var group in exp.Factory.GetParameterGroups(exp.ExperimentClass))
                foreach (var parameter in group.GetParameters())
                    ((IDictionary<string, object>)expandoContext)[parameter.Key] = context.TryGet(parameter, out var val) ? val : parameter.DefaultValue;
            }
            var stringBuilder = new StringBuilder();
            var offset = 0;
            void OffsetTo(int newOffset, bool write)
            {
                if (newOffset <= offset) return;
                if (write)
                    stringBuilder.Append(sessionName.Substring(offset, newOffset - offset));
                offset = newOffset;
            }
           
            while (true)
            {
                var open = sessionName.IndexOf("{{", offset, StringComparison.Ordinal);
                if (open == -1) break;
                OffsetTo(open, true);
                var close = sessionName.IndexOf("}}", open + 2, StringComparison.Ordinal);
                if (close == -1)
                {
                    OffsetTo(open + 2, true);
                    continue;
                }
                OffsetTo(close + 2, false);
                var script = sessionName.Substring(open + 2, close - open - 2);
                try
                {
                    stringBuilder.Append(DslExpressionFactory.Create(script).Evaluate((IDictionary<string, object>)expandoContext));
                }
                catch (Exception e)
                {
                    Logger.Warn("GetFormattedSessionDescriptor", e, "sessionName", sessionName, "script", script);
                    throw new UserException($"Failed to evaluate script: '{script}', session name: '{sessionName}'", e);
                }
            }
            OffsetTo(sessionName.Length, true);
            return stringBuilder.ToString();
        }

    }

}
