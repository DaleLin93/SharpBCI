using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.UI;

namespace SharpBCI.Extensions.Data
{

    public static class ColorKeys
    {
        public const string Background = nameof(Background);
        public const string Foreground = nameof(Foreground);
        public const string BorderColor = nameof(BorderColor);
    }

    [ParameterizedObject(typeof(Factory))]
    public struct Colors : IParameterizedObject
    {

        public class Factory : ParameterizedObjectFactory<Colors>
        {

            public static readonly NamedProperty<string[]> ColorKeysProperty = new NamedProperty<string[]>("ColorKeys", new[] {ColorKeys.Background, ColorKeys.Foreground});

            private static readonly Color DefaultColor = new Color();

            private readonly IDictionary<string, Parameter<Color>> _cachedParameters = new Dictionary<string, Parameter<Color>>();

            public override IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter) =>
                ColorKeysProperty.Get(parameter.Metadata).Select(GetParameter).ToArray<IParameterDescriptor>();

            public override Colors Create(IParameterDescriptor parameter, IReadonlyContext context)
            {
                var colorKeys = ColorKeysProperty.Get(parameter.Metadata);
                var colors = new uint[colorKeys.Length];
                for (var i = 0; i < colorKeys.Length; i++)
                    colors[i] = GetParameter(colorKeys[i]).Get(context).ToUIntArgb();
                return new Colors(colorKeys, colors);
            }

            public override IReadonlyContext Parse(IParameterDescriptor parameter, Colors colors)
            {
                var context = new Context();
                var colorKeys = ColorKeysProperty.Get(parameter.Metadata);
                for (var i = 0; i < colorKeys.Length; i++)
                    GetParameter(colorKeys[i]).Set(context, colors[i].ToSdColor());
                return context;
            }

            private Parameter<Color> GetParameter(string name) => _cachedParameters.GetOrCreate(name, key => new Parameter<Color>(name, DefaultColor));

        }

        [CanBeNull]
        private readonly KeyValuePair<string, uint>[] _pairs;

        public Colors(params Color[] colors) : this(null, colors.Select(c => c.ToUIntArgb()).ToArray()) { }

        public Colors(params uint[] colors) : this(null, colors) { }

        public Colors(string[] keys, uint[] colors)
        {
            keys = keys ?? EmptyArray<string>.Instance;
            colors = colors ?? EmptyArray<uint>.Instance;
            _pairs = new KeyValuePair<string, uint>[colors.Length];
            for (var i = 0; i < colors.Length; i++)
                _pairs[i] = new KeyValuePair<string, uint>(i < 0 || i >= keys.Length ? null : keys[i], colors[i]);
        }

        public Colors(KeyValuePair<string, uint>[] keyedColors) : this(keyedColors, true) { }

        private Colors(KeyValuePair<string, uint>[] keyedColors, bool clone) => _pairs = clone ? (KeyValuePair<string, uint>[])keyedColors?.Clone() : keyedColors;

        public uint this[int index] => GetOrDefault(index, 0);

        public uint this[string name] => GetOrDefault(name, 0);

        public int Count => _pairs?.Length ?? 0;

        public uint GetOrDefault(int index, uint defaultValue) => _pairs == null || index < 0 || index >= _pairs.Length ? defaultValue : _pairs[index].Value;

        public uint GetOrDefault(string name, uint defaultValue)
        {
            if (_pairs != null)
                foreach (var pair in _pairs)
                    if (Equals(pair.Key, name))
                        return pair.Value;
            return defaultValue;
        }

    }

}