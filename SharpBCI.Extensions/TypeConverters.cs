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

        public static Parameter<ArrayQuery>.Builder SetDefaultQuery(this Parameter<ArrayQuery>.Builder builder, string query)
        {
            var converter = ArrayQuery.TypeConverter;
            return builder.SetDefaultValue(converter.ConvertBackward(query)).SetTypeConverter(converter);
        }

        public static Parameter<ArrayQuery<double>>.Builder SetDefaultQuery(this Parameter<ArrayQuery<double>>.Builder builder, string query)
        {
            var converter = ArrayQuery<double>.CreateTypeConverter(IdentityTypeConverter<double>.Instance);
            return builder.SetDefaultValue(converter.ConvertBackward(query)).SetTypeConverter(converter);
        }

        public static Parameter<ArrayQuery<T>>.Builder SetDefaultQuery<T>(this Parameter<ArrayQuery<T>>.Builder builder, string query,
            ITypeConverter<double, T> numberConverter)
        {
            var converter = ArrayQuery<T>.CreateTypeConverter(numberConverter);
            return builder.SetDefaultValue(converter.ConvertBackward(query)).SetTypeConverter(converter);
        }

        public static Parameter<MatrixQuery>.Builder SetDefaultQuery(this Parameter<MatrixQuery>.Builder builder, string query)
        {
            var converter = MatrixQuery.TypeConverter;
            return builder.SetDefaultValue(converter.ConvertBackward(query)).SetTypeConverter(converter);
        }

        public static Parameter<MatrixQuery<double>>.Builder SetDefaultQuery(this Parameter<MatrixQuery<double>>.Builder builder, string query)
        {
            var converter = MatrixQuery<double>.CreateTypeConverter(IdentityTypeConverter<double>.Instance);
            return builder.SetDefaultValue(converter.ConvertBackward(query)).SetTypeConverter(converter);
        }

        public static Parameter<MatrixQuery<T>>.Builder SetDefaultQuery<T>(this Parameter<MatrixQuery<T>>.Builder builder, string query,
            ITypeConverter<double, T> numberConverter)
        {
            var converter = MatrixQuery<T>.CreateTypeConverter(numberConverter);
            return builder.SetDefaultValue(converter.ConvertBackward(query)).SetTypeConverter(converter);
        }

    }

}
