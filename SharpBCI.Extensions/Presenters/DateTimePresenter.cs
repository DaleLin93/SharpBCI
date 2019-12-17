using System;
using System.Windows.Controls;

namespace SharpBCI.Extensions.Presenters
{

    public class DateTimePresenter : IPresenter
    {

        private class Accessor : IPresentedParameterAccessor
        {

            private readonly IParameterDescriptor _parameter;

            private readonly DatePicker _datePicker;

            public Accessor(IParameterDescriptor parameter, DatePicker datePicker)
            {
                _parameter = parameter;
                _datePicker = datePicker;
            }

            public object Value
            {
                get => _parameter.IsValidOrThrow(_datePicker.SelectedDate);
                set => _datePicker.SelectedDate = (DateTime?) value;
            }

        }

        public static readonly DateTimePresenter Instance = new DateTimePresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var picker = new DatePicker();
            picker.SelectedDateChanged += (sender, args) => updateCallback();
            return new PresentedParameter(param, picker, new Accessor(param, picker), picker);
        }

    }
}