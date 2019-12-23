using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using MarukoLib.Lang;
using MarukoLib.UI;

namespace SharpBCI.Extensions
{

    public static class TypeConverters
    {

        public static readonly TypeConverter<Color, uint> SdColor2UInt =
            TypeConverter<Color, uint>.Of(sdColor => sdColor.ToUIntArgb(), uintColor => uintColor.ToSdColor());

        public static readonly ITypeConverter<double, float> Double2Float = TypeConverter<double, float>.Of(dVal => (float)dVal, fVal => fVal);

        public static readonly ITypeConverter<long, int> Long2Int = TypeConverter<long, int>.Of(lVal => (int)lVal, iVal => iVal);

        public static readonly ITypeConverter<double, int> Double2Int = TypeConverter<double, int>.Of(dVal => (int)dVal, iVal => iVal);

        public static readonly ITypeConverter<double, long> Double2Long = TypeConverter<double, long>.Of(dVal => (long)dVal, lVal => lVal);

        public static readonly ITypeConverter<int, float> Int2Float = TypeConverter<int, float>.Of(iVal => iVal, fVal => (int)fVal);

        public static readonly ITypeConverter<long, float> Long2Float = TypeConverter<long, float>.Of(lVal => lVal, fVal => (long)fVal);

        private static readonly IReadOnlyCollection<ITypeConverter> Converters;

        static TypeConverters()
        {
            Converters = (from fieldInfo in typeof(TypeConverters).GetFields()
                    where typeof(ITypeConverter).IsAssignableFrom(fieldInfo.FieldType)
                    select (ITypeConverter)fieldInfo.GetValue(null))
                .ToList();
        }

        public static ITypeConverter<T1, T2> CreateBiDirectionConverter<T1, T2>(IEnumerable<KeyValuePair<T1, T2>> pairs,
            out IReadOnlyDictionary<T1, T2> dictionary1, out IReadOnlyDictionary<T2, T1> dictionary2)
        {
            var dict1 = new Dictionary<T1, T2>();
            var dict2 = new Dictionary<T2, T1>();
            dictionary1 = dict1;
            dictionary2 = dict2;
            foreach (var pair in pairs)
            {
                dict1[pair.Key] = pair.Value;
                dict2[pair.Value] = pair.Key;
            }
            return TypeConverter<T1, T2>.Of(t1 => dict1[t1], t2 => dict2[t2]);
        }

        public static ITypeConverter<string, T> CreateNamedConverter<T>(IEnumerable<T> values, out IReadOnlyDictionary<string, T> dictionary) where T : INamed
        {
            var dict = new Dictionary<string, T>();
            dictionary = dict;
            foreach (var value in values) dict[value.Name] = value;
            return TypeConverter<string, T>.Of(str => dict[str], val => val.Name);
        }

        public static bool FindConverter<T1, T2>(out ITypeConverter<T1, T2> converter)
        {
            if (FindConverter(typeof(T1), typeof(T2), out var rawConverter))
            {
                converter = rawConverter.As<T1, T2>();
                return true;
            }
            converter = null;
            return false;
        }

        public static bool FindConverter(Type inputType, Type outputType, out ITypeConverter converter)
        {
            foreach (var current in Converters)
            {
                if (current.IsExactlyMatch(inputType, outputType))
                {
                    converter = current;
                    return true;
                }
                if (current.IsExactlyMatch(outputType, inputType))
                {
                    converter = current.Inverse();
                    return true;
                }
            }
            converter = default;
            return false;
        }
        
    }

}
