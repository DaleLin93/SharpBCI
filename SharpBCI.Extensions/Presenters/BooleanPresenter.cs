using System;
using System.Windows;
using System.Windows.Controls;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Presenters
{

    public class BooleanPresenter : IPresenter
    {

        private class Accessor : IPresentedParameterAccessor
        {

            private readonly IParameterDescriptor _parameter;

            private readonly CheckBox _checkBox;

            public Accessor(IParameterDescriptor parameter, CheckBox checkBox)
            {
                _parameter = parameter;
                _checkBox = checkBox;
            }

            public object GetValue() => _parameter.IsValidOrThrow(_checkBox.IsChecked ?? false);

            public void SetValue(object value) => _checkBox.IsChecked = (bool?)value ?? false;

        }

        public static readonly NamedProperty<string> CheckboxTextProperty = new NamedProperty<string>("CheckBoxText");

        public static readonly BooleanPresenter Instance = new BooleanPresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            void StateChangeHandler(object sender, RoutedEventArgs args) => updateCallback();
            var checkBox = new CheckBox
            {
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center
            };
            if (CheckboxTextProperty.TryGet(param.Metadata, out var checkboxText)) checkBox.Content = checkboxText;
            checkBox.Checked += StateChangeHandler;
            checkBox.Unchecked += StateChangeHandler;
            return new PresentedParameter(param, checkBox, new Accessor(param, checkBox), checkBox);
        }

    }

}