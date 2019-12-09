using System;
using System.Collections.Generic;
using MarukoLib.Lang;
using MarukoLib.Logging;
using MarukoLib.Persistence;
using SharpBCI.Extensions.Data;

namespace SharpBCI.Extensions
{
    
    public static class PersistenceHelper
    {

        public const string NullPlaceholder = "{NULL}";

        private static readonly Logger Logger = Logger.GetLogger(typeof(PersistenceHelper));

        public static ContextProperty<ITypeConverter> PersistentTypeConverterProperty = new ContextProperty<ITypeConverter>();

        public static bool TryGetPersistentTypeConverter(this IParameterDescriptor parameter, out ITypeConverter converter) => 
            PersistentTypeConverterProperty.TryGet(parameter.Metadata, out converter) && converter != null;

        public static IDictionary<string, string> SerializeArgs(this IEnumerable<IParameterDescriptor> parameters, IReadonlyContext context)
        {
            if (context == null) return null;
            var @params = new Dictionary<string, string>();
            foreach (var p in parameters)
                if (context.TryGet(p, out var val))
                    try { @params[p.Key] = p.SerializeParam(val); }
                    catch (Exception e) { Logger.Warn("SerializeArgs", e, "param", p.Key, "value", val); }
            return @params;
        }

        public static IContext DeserializeArgs(this IEnumerable<IParameterDescriptor> parameters, IDictionary<string, string> input)
        {
            if (input == null) return null;
            var context = new Context();
            foreach (var p in parameters)
                if (input.ContainsKey(p.Key))
                    try { context.Set(p, p.DeserializeParam(input[p.Key])); }
                    catch (Exception e) { Logger.Warn("DeserializeArgs", e, "param", p.Key, "value", input[p.Key]); }
            return context;
        }

        public static string SerializeParam(this IParameterDescriptor parameter, object value)
        {
            if (value == null) return null;
            if (TryGetPersistentTypeConverter(parameter, out var converter))
                return JsonUtils.Serialize(converter.ConvertForward(value));
            if (typeof(IParameterizedObject).IsAssignableFrom(parameter.ValueType))
            {
                var factory = parameter.GetParameterizedObjectFactory();
                var context = factory.Parse(parameter, (IParameterizedObject)value);
                var output = new Dictionary<string, string>();
                foreach (var p in factory.GetParameters(parameter))
                    if (context.TryGet(p, out var val))
                        output[p.Key] = SerializeParam(p, val);
                return JsonUtils.Serialize(output);
            }
            return JsonUtils.Serialize(value);
        }

        public static object DeserializeParam(this IParameterDescriptor parameter, string value)
        {
            if (TryGetPersistentTypeConverter(parameter, out var converter))
                return converter.ConvertBackward(JsonUtils.Deserialize(value, converter.OutputType));
            if (typeof(IParameterizedObject).IsAssignableFrom(parameter.ValueType))
            {
                var factory = parameter.GetParameterizedObjectFactory();
                var context = new Context();
                var strParams = JsonUtils.Deserialize<Dictionary<string, string>>(value);
                foreach (var p in factory.GetParameters(parameter))
                    context.Set(p, DeserializeParam(p, strParams[p.Key]));
                return factory.Create(parameter, context);
            }
            return value == null ? null : JsonUtils.Deserialize(value, parameter.ValueType);
        }
        
    }

}
