using JetBrains.Annotations;
using System;
using System.Collections.Generic;
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

        public string DeviceType;

        public ParameterizedEntity Device;

        public ParameterizedEntity[] Consumers;

        public DeviceParams(string deviceType, ParameterizedEntity device, ParameterizedEntity[] consumers)
        {
            DeviceType = deviceType;
            Device = device;
            Consumers = consumers;
        }

    }

    public struct MultiSessionConfig
    {

        public struct SessionItem
        {

            public string SessionDescriptor;

            public string ParadigmConfigPath;

        }

        public const string FileSuffix = ".mscfg";

        public string Subject;

        public SessionItem[] Sessions;

        public DeviceParams[] Devices;

    }

    public struct SessionConfig
    {

        public const string FileSuffix = ".scfg";

        public string Subject;

        public string SessionDescriptor;

        public ParameterizedEntity Paradigm;

        public DeviceParams[] Devices;

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
                    for (;;)
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

        public static string GetFullSessionName(this SessionConfig sessionConfig, long? time = null) => 
            GetFullSessionName(sessionConfig.Subject, sessionConfig.SessionDescriptor, sessionConfig.Paradigm, time);

        public static string GetFullSessionName(string subject, string sessionDescriptor, ParameterizedEntity paradigmEntity, long? time = null)
        {
            IReadonlyContext context = null;
            if (paradigmEntity.Params != null && App.Instance.Registries.Registry<PluginParadigm>().LookUp(paradigmEntity.Id, out var paradigm))
                context = paradigm.DeserializeParams(paradigmEntity.Params);
            return GetFullSessionName(subject, sessionDescriptor, context, time);
        }

        public static string GetFullSessionName(string subject, string sessionDescriptor, IReadonlyContext context, long? time = null)
        {
            if (context != null) sessionDescriptor = StringInterpolation(sessionDescriptor, context);
            return Session.GetFullSessionName(time, subject, sessionDescriptor);
        }

        public static string StringInterpolation(string template, IReadonlyContext context)
        {
            dynamic expandoContext = new ExpandoObject();
            foreach (var property in context.Properties)
                if (property is IParameterDescriptor parameter)
                    ((IDictionary<string, object>)expandoContext)[parameter.Key] = context.TryGet(parameter, out var val) ? val : parameter.DefaultValue;

            var stringBuilder = new StringBuilder();
            var offset = 0;
            void OffsetTo(int newOffset, bool write)
            {
                if (newOffset <= offset) return;
                if (write)
                    stringBuilder.Append(template.Substring(offset, newOffset - offset));
                offset = newOffset;
            }
           
            for (;;)
            {
                var open = template.IndexOf("{{", offset, StringComparison.Ordinal);
                if (open == -1) break;
                OffsetTo(open, true);
                var close = template.IndexOf("}}", open + 2, StringComparison.Ordinal);
                if (close == -1)
                {
                    OffsetTo(open + 2, true);
                    continue;
                }
                OffsetTo(close + 2, false);
                var script = template.Substring(open + 2, close - open - 2);
                try
                {
                    stringBuilder.Append(DslExpressionFactory.Create(script).Evaluate((IDictionary<string, object>)expandoContext));
                }
                catch (Exception e)
                {
                    Logger.Warn("GetInterpolatedString", e, "template", template, "script", script);
                    throw new UserException($"Failed to evaluate script: '{script}', template: '{template}'", e);
                }
            }
            OffsetTo(template.Length, true);
            return stringBuilder.ToString();
        }

    }

}
