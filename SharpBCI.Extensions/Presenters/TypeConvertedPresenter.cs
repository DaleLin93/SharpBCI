using System;
using System.Windows;

namespace SharpBCI.Extensions.Presenters
{
    public class TypeConvertedPresenter : IPresenter
    {

        public static readonly TypeConvertedPresenter Instance = new TypeConvertedPresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            if (!param.TryGetPresentTypeConverter(out var converter)) throw new ArgumentException();
            var converted = new TypeConvertedParameter(param, converter);
            var presented = converted.GetPresenter().Present(converted, updateCallback);
            void Setter(object val) => presented.Delegates.Setter(converter.ConvertForward(val));
            object Getter() => converter.ConvertBackward(presented.Delegates.Getter());
            bool Validator(object val) => presented.Delegates.Validator?.Invoke(converter.ConvertForward(val)) ?? true;
            return new PresentedParameter(param, presented.Element, new PresentedParameter.ParamDelegates(Getter, Setter, Validator, presented.Delegates.Updater));
        }

    }

}