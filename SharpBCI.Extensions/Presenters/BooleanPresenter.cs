using System;
using System.Windows;
using System.Windows.Controls;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Presenters
{

    public class BooleanPresenter : IPresenter
    {

        public static readonly NamedProperty<string> CheckboxTextProperty = new NamedProperty<string>("CheckBoxText");

        public static readonly BooleanPresenter Instance = new BooleanPresenter();

        public PresentedParameter Present(Window window, IParameterDescriptor param, Action updateCallback)
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
            void Setter(object val) => checkBox.IsChecked = (bool?)val ?? false;
            object Getter() => checkBox.IsChecked ?? false;
            void Updater(ParameterStateType state, bool value)
            {
                switch (state)
                {
                    case ParameterStateType.Enabled:
                        checkBox.IsEnabled = value;
                        break;
                }
            }
            return new PresentedParameter(param, checkBox, new PresentedParameter.ParamDelegates(Getter, Setter, param.IsValid, Updater));
        }

    }

}