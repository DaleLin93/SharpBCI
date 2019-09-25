using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MarukoLib.Lang;
using MarukoLib.Lang.Concurrent;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    public class RangePresenter : IPresenter
    {

        private class Accessor : IPresentedParameterAccessor
        {

            private readonly IParameterDescriptor _parameter;

            private readonly Func<double, string> _formatter;

            private readonly Slider _slider;

            public Accessor(IParameterDescriptor parameter, Func<double, string> formatter, Slider slider)
            {
                _parameter = parameter;
                _formatter = formatter;
                _slider = slider;
            }

            public object GetValue() => _parameter.IsValidOrThrow(new Range(_slider.SelectionStart, _slider.SelectionEnd));

            public void SetValue(object value)
            {
                if (value is Range interval)
                {
                    _slider.SelectionStart = interval.MinValue;
                    _slider.SelectionEnd = interval.MaxValue;
                    _slider.Value = interval.MaxValue;
                    UpdateToolTip();
                }
            }

            internal void UpdateToolTip() => _slider.ToolTip = $"{_formatter(_slider.SelectionStart)} ~ {_formatter(_slider.SelectionEnd)}";

        }

        /// <summary>
        /// Default Value: 0
        /// </summary>
        public static readonly NamedProperty<double> MinimumValueProperty = SliderNumberPresenter.MinimumValueProperty;

        /// <summary>
        /// Default Value: 100
        /// </summary>
        public static readonly NamedProperty<double> MaximumValueProperty = SliderNumberPresenter.MaximumValueProperty;

        /// <summary>
        /// Default Value: 1
        /// </summary>
        public static readonly NamedProperty<double> TickFrequencyProperty = SliderNumberPresenter.TickFrequencyProperty;

        /// <summary>
        /// Default Value: $"{num:G}"
        /// </summary>
        public static readonly NamedProperty<Func<double, string>> NumberFormatterProperty = SliderNumberPresenter.NumberFormatterProperty;

        /// <summary>
        /// Default Value: None
        /// </summary>
        public static readonly NamedProperty<TickPlacement> TickPlacementProperty = SliderNumberPresenter.TickPlacementProperty;

        public static readonly RangePresenter Instance = new RangePresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var numberFormatter = NumberFormatterProperty.Get(param.Metadata);
            var valueFormatter = string.IsNullOrWhiteSpace(param.Unit)? numberFormatter : (val => $"{numberFormatter(val)} {param.Unit}");
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.MinorSpacingGridLength});
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.Star1GridLength});
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.MinorSpacingGridLength});
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
            var slider = new Slider
            {
                Minimum = MinimumValueProperty.GetOrDefault(param.Metadata, 0),
                Maximum = MaximumValueProperty.GetOrDefault(param.Metadata, 100),
                TickFrequency = TickFrequencyProperty.Get(param.Metadata),
                TickPlacement = TickPlacementProperty.Get(param.Metadata),
                AutoToolTipPlacement = AutoToolTipPlacement.BottomRight,
                IsSelectionRangeEnabled = true,
                IsSnapToTickEnabled = true,
            };
            var minimumTextBlock = new TextBlock {Text = $"{numberFormatter(slider.Minimum)}"};
            var maximumTextBlock = new TextBlock {Text = $"{numberFormatter(slider.Maximum)}"};
            Grid.SetColumn(minimumTextBlock, 0);
            Grid.SetColumn(slider, 2);
            Grid.SetColumn(maximumTextBlock, 4);
            grid.Children.Add(minimumTextBlock);
            grid.Children.Add(slider);
            grid.Children.Add(maximumTextBlock);

            var accessor = new Accessor(param, valueFormatter, slider);
            var selectionStartValue = new AtomicDouble();
            slider.GotMouseCapture += (sender, e) => selectionStartValue.Set(((Slider) sender).Value);
            slider.ValueChanged += (sender, e) =>
            {
                var slider0 = (Slider) sender;
                if (!slider0.IsMouseCaptureWithin) return;
                var v1 = selectionStartValue.Value;
                var v2 = slider0.Value;
                slider0.SelectionStart = Math.Min(v1, v2);
                slider0.SelectionEnd = Math.Max(v1, v2);
            };
            slider.LostMouseCapture += (sender, e) =>
            {
                accessor.UpdateToolTip();
                updateCallback();
            };
            // ReSharper disable once ImplicitlyCapturedClosure
            slider.MouseRightButtonDown += (sender, e) =>
            {
                ((Slider)sender).SelectionStart = ((Slider)sender).SelectionEnd = ((Slider)sender).Value;
                accessor.UpdateToolTip();
            };
            return new PresentedParameter(param, grid, accessor, slider);
        }

    }

}