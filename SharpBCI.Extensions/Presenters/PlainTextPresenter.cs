using System;
using System.Text.RegularExpressions;
using System.Windows.Controls;
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

            public object GetValue()
            {
                var text = _textBox.Text ?? "";
                if (_regex != null && !_regex.IsMatch(text)) throw new Exception("input text not match the given pattern");
                return _parameter.IsValidOrThrow(_parameter.ParseValueFromString(text));
            }

            public void SetValue(object value) => _textBox.Text = _parameter.ConvertValueToString(value ?? "");

        }

        public static readonly NamedProperty<Regex> PatternProperty = new NamedProperty<Regex>("Pattern");

        public static readonly PlainTextPresenter Instance = new PlainTextPresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var regex = PatternProperty.GetOrDefault(param.Metadata);
            var textBox = new TextBox { MaxLength = param.ValueType == typeof(char) ? 1 : 128 };
            textBox.TextChanged += (sender, args) => updateCallback();
            return new PresentedParameter(param, textBox, new Accessor(param, regex, textBox), textBox);
        }

    }
}