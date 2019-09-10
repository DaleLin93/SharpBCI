using System;
using System.Windows;

namespace SharpBCI.Extensions.Presenters
{
    public class TypeConvertedPresenter : IPresenter
    {

        public static readonly TypeConvertedPresenter Instance = new TypeConvertedPresenter();

        public PresentedParameter Present(Window window, IParameterDescriptor param, Action updateCallback)
        {
            var converted = param.GetTypeConvertedParameter();
            var presented = converted.GetPresenter().Present(window, converted, updateCallback);
            void Setter(object val) => presented.Delegates.Setter(param.TypeConverter.ConvertForward(val));
            object Getter() => param.TypeConverter.ConvertBackward(presented.Delegates.Getter());
            bool Validator(object val) => presented.Delegates.Validator?.Invoke(param.TypeConverter.ConvertForward(val)) ?? true;
            return new PresentedParameter(param, presented.Element, new PresentedParameter.ParamDelegates(Getter, Setter, Validator, presented.Delegates.Updater));
        }

    }
}