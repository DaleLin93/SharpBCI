using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
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

        public static IDictionary<string, string> SerializeParams(this IEnumerable<IParameterDescriptor> parameters, IReadonlyContext context)
        {
            if (context == null) return null;
            var @params = new Dictionary<string, string>();
            foreach (var p in parameters)
                if (context.TryGet(p, out var val))
                    try { @params[p.Key] = p.SerializeParam(val); }
                    catch (Exception e) { Logger.Warn("SerializeParams", e, "param", p.Key, "value", val); }
            return @params;
        }

        public static IContext DeserializeParams(this IEnumerable<IParameterDescriptor> parameters, IDictionary<string, string> input)
        {
            if (input == null) return null;
            var context = new Context();
            foreach (var p in parameters)
                if (input.ContainsKey(p.Key))
                    try { context.Set(p, p.DeserializeParam(input[p.Key])); }
                    catch (Exception e) { Logger.Warn("DeserializeParams", e, "param", p.Key, "value", input[p.Key]); }
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

        public static object ParseValueFromString(this IParameterDescriptor parameter, string strVal)
        {
            if (Equals(NullPlaceholder, strVal)) return null;
            return TryGetPersistentTypeConverter(parameter, out var converter) 
                ? converter.ConvertBackward(ParseValue(converter.OutputType, strVal)) 
                : ParseValue(parameter.ValueType, strVal);
        }

        [SuppressMessage("ReSharper", "AssignNullToNotNullAttribute")]
        private static object ParseValue(Type type, string strVal)
        {
            if (Equals(NullPlaceholder, strVal)) return null;
            if (type.IsArray)
                if (type.GetArrayRank() == 1)
                {
                    strVal = strVal.Trim();
                    var substrings = strVal.Split(' ').Where(str => !str.IsBlank()).ToArray();
                    var array = Array.CreateInstance(type.GetElementType(), substrings.Length);
                    for (var i = 0; i < substrings.Length; i++)
                        array.SetValue(ParseValue(type.GetElementType(), substrings[i]), i);
                    return array;
                }
                else
                    throw new NotSupportedException("Only 1D-array was supported");

            if (type == typeof(string)) return strVal;

            var nullableType = type.IsNullableType(out var underlyingType);
            var actualType = nullableType ? underlyingType : type;

            if (actualType.IsEnum)
            {
                var enumValues = Enum.GetValues(actualType);
                foreach (var enumValue in enumValues)
                    if (Equals(enumValue.ToString(), strVal))
                        return enumValue;
                throw new ArgumentException($"{actualType.Name} value not found by name: '{strVal}'");
            }

            if (!actualType.IsPrimitive) throw new ArgumentException("type is not supported, type: " + type.FullName);

            if (strVal?.IsEmpty() ?? true)
                if (nullableType)
                    return null;
                else
                    throw new ArgumentException("cannot convert empty string to type: " + type.FullName);

            if (actualType == typeof(char)) return strVal[0];
            if (actualType == typeof(byte)) return byte.Parse(strVal);
            if (actualType == typeof(sbyte)) return sbyte.Parse(strVal);
            if (actualType == typeof(short)) return short.Parse(strVal);
            if (actualType == typeof(ushort)) return ushort.Parse(strVal);
            if (actualType == typeof(int)) return int.Parse(strVal);
            if (actualType == typeof(uint)) return uint.Parse(strVal);
            if (actualType == typeof(ulong)) return ulong.Parse(strVal);
            if (actualType == typeof(float)) return float.Parse(strVal);
            if (actualType == typeof(double)) return double.Parse(strVal);
            if (actualType == typeof(decimal)) return decimal.Parse(strVal);

            throw new Exception("unreachable statement");
        }

        public static string ConvertValueToString(this IParameterDescriptor parameter, object val)
        {
            if (TryGetPersistentTypeConverter(parameter, out var converter)) val = converter.ConvertForward(val);
            return val == null ? NullPlaceholder : ConvertValueToString(val.GetType(), val);
        }

        public static string ConvertValueToString(this Type type, object value)
        {
            if (type.IsArray)
            {
                if (type.GetArrayRank() == 1 && (type.GetElementType()?.IsPrimitive ?? false))
                {
                    var stringBuilder = new StringBuilder();
                    var array = (Array) value;
                    for (var i = 1; i <= array.Length; i++)
                    {
                        stringBuilder.Append(array.GetValue(i - 1));
                        if (i != array.Length) stringBuilder.Append(' ');
                    }
                    return stringBuilder.ToString();
                }
                throw new NotSupportedException();
            }
            if (value is IDescribable describable) return describable.GetShortDescription();
            return value.ToString();
        }

    }

}
