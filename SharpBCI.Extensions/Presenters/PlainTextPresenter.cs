using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Presenters
{
    public class PlainTextPresenter : IPresenter
    {

        private class Accessor : IPresentedParameterAccessor
        {

            private readonly IParameterDescriptor _parameter;

            private readonly Regex _regex;

            private readonly TextBox _textBox;

            public Accessor(IParameterDescriptor parameter, Regex regex, TextBox textBox)
            {
                _parameter = parameter;
                _regex = regex;
                _textBox = textBox;
            }

            public object Value
            {
                get
                {
                    var text = _textBox.Text ?? "";
                    if (_regex != null && !_regex.IsMatch(text)) throw new Exception("input text not match the given pattern");
                    return _parameter.IsValidOrThrow(_parameter.ParseValueFromString(text));
                }
                set => _textBox.Text = _parameter.ConvertValueToString(value ?? "");
            }

        }

        public static readonly NamedProperty<Regex> PatternProperty = new NamedProperty<Regex>("Pattern");

        public static readonly NamedProperty<double> TextBoxHeightProperty = new NamedProperty<double>("TextBoxHeight", double.NaN);

        public static readonly NamedProperty<int> MaxLengthProperty = new NamedProperty<int>("MaxLength");

        public static readonly NamedProperty<bool> MultiLineProperty = new NamedProperty<bool>("MultiLine", false);

        public static readonly NamedProperty<TextAlignment> TextAlignmentProperty = new NamedProperty<TextAlignment>("TextAlignment");

        public static readonly NamedProperty<TextWrapping> TextWrappingProperty = new NamedProperty<TextWrapping>("TextWrapping");

        public static readonly NamedProperty<double> FontSizeProperty = new NamedProperty<double>("FontSize");

        public static readonly NamedProperty<Brush> ForegroundProperty = new NamedProperty<Brush>("Foreground");

        public static readonly PlainTextPresenter Instance = new PlainTextPresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var regex = PatternProperty.GetOrDefault(param.Metadata);
            var textBox = new TextBox
            {
                MaxLength = MaxLengthProperty.GetOrDefault(param.Metadata, param.ValueType == typeof(char) ? 1 : 128),
                AcceptsReturn = MultiLineProperty.Get(param.Metadata),
            };
            if (TextBoxHeightProperty.TryGet(param.Metadata, out var height)) textBox.Height = height;
            if (TextAlignmentProperty.TryGet(param.Metadata, out var textAlignment)) textBox.TextAlignment = textAlignment;
            if (TextWrappingProperty.TryGet(param.Metadata, out var textWrapping)) textBox.TextWrapping = textWrapping;
            if (FontSizeProperty.TryGet(param.Metadata, out var fontSize)) textBox.FontSize = fontSize;
            if (ForegroundProperty.TryGet(param.Metadata, out var foreground)) textBox.Foreground = foreground;
            textBox.TextChanged += (sender, args) => updateCallback();
            return new PresentedParameter(param, textBox, new Accessor(param, regex, textBox), textBox);
        }

    }
}