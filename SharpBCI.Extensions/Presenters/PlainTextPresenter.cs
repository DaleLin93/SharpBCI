using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{
    [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
    public class PlainTextPresenter : IPresenter
    {

        public static readonly PlainTextPresenter Instance = new PlainTextPresenter();

        public PresentedParameter Present(Window window, IParameterDescriptor param, Action updateCallback)
        {
            var textBox = new TextBox { MaxLength = param.ValueType == typeof(char) ? 1 : 128 };
            textBox.TextChanged += (sender, args) => updateCallback();
            void Setter(object val) => textBox.Text = param.ValueToString(val ?? "");
            object Getter() => param.ParseValue(textBox.Text ?? "");
            void Updater(ParameterStateType state, bool value)
            {
                switch (state)
                {
                    case ParameterStateType.Enabled:
                        textBox.IsEnabled = value;
                        break;
                    case ParameterStateType.Valid:
                        textBox.Background = value ? Brushes.Transparent : new SolidColorBrush(ViewConstants.InvalidColor);
                        break;
                }
            }
            return new PresentedParameter(param, textBox, new PresentedParameter.ParamDelegates(Getter, Setter, param.IsValid, Updater));
        }

    }
}