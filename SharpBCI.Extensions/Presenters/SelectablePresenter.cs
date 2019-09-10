using System;
using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;

namespace SharpBCI.Extensions.Presenters
{
    [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
    public class SelectablePresenter : IPresenter
    {

        public static readonly SelectablePresenter Instance = new SelectablePresenter();

        public static ContextProperty<Func<IParameterDescriptor, IEnumerable>> SelectableValuesFuncProperty = 
            new ContextProperty<Func<IParameterDescriptor, IEnumerable>>();

        public PresentedParameter Present(Window window, IParameterDescriptor param, Action updateCallback)
        {
            string ToStrFunc(object value) => param.ValueToString(value);
            IEnumerable items;
            if (SelectableValuesFuncProperty.TryGet(param.Metadata, out var selectableValuesFunc))
                items = selectableValuesFunc(param);
            else if (param.IsSelectable())
                items = param.SelectableValues;
            else
                throw new ProgrammingException("Parameter.SelectableValues or SelectablePresenter.SelectableValuesFuncProperty must be assigned");

            var comboBox = new ComboBox {ItemsSource = ToStringOverridenWrapper.Of(items, ToStrFunc)};
            comboBox.SelectionChanged += (sender, args) => updateCallback();
            void Setter(object val) => comboBox.SelectedValue = ToStringOverridenWrapper.Wrap(val, ToStrFunc);
            object Getter() => ToStringOverridenWrapper.TryUnwrap(comboBox.SelectedValue);
            void Updater(ParameterStateType state, bool value)
            {
                if (state == ParameterStateType.Enabled) comboBox.IsEnabled = value;
            }
            return new PresentedParameter(param, comboBox, new PresentedParameter.ParamDelegates(Getter, Setter, value => value != null, Updater));
        }

    }
}