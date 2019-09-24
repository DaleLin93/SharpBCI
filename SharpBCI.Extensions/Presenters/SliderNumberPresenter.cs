using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    /// <summary>
    /// Notice: MinimumValueProperty & MaximumValueProperty must be set to use this presenter.
    /// </summary>
    public class SliderNumberPresenter : IPresenter
    {

        private class Accessor : IPresentedParameterAccessor
        {

            private readonly IParameterDescriptor _parameter;

            private readonly Slider _slider;

            public Accessor(IParameterDescriptor parameter, Slider slider)
            {
                _parameter = parameter;
                _slider = slider;
            }

            public object GetValue() => _parameter.IsValidOrThrow(_slider.Value);

            public void SetValue(object value) => _slider.Value = (double)value;

        }

        /// <summary>
        /// Required
        /// </summary>
        public static readonly NamedProperty<double> MinimumValueProperty = new NamedProperty<double>("MaximumValue");

        /// <summary>
        /// Required
        /// </summary>
        public static readonly NamedProperty<double> MaximumValueProperty = new NamedProperty<double>("MaximumValue");

        /// <summary>
        /// Default Value: 1
        /// </summary>
        public static readonly NamedProperty<double> TickFrequencyProperty = new NamedProperty<double>("TickFrequency", 1);

        /// <summary>
        /// Default Value: $"{num:G}"
        /// </summary>
        public static readonly NamedProperty<Func<double, string>> NumberFormatterProperty = new NamedProperty<Func<double, string>>("NumberFormatter", num => $"{num:G}");

        /// <summary>
        /// Default Value: None
        /// </summary>
        public static readonly NamedProperty<TickPlacement> TickPlacementProperty = new NamedProperty<TickPlacement>("TickPlacement", TickPlacement.None);

        public static readonly SliderNumberPresenter Instance = new SliderNumberPresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            if (!param.Metadata.Contains(MinimumValueProperty) || !param.Metadata.Contains(MaximumValueProperty))
                throw new ProgrammingException($"Missing 'MinimumValue' or 'MaximumValue' for parameter '{param.Name}'");
            var numberFormatter = NumberFormatterProperty.Get(param.Metadata);
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.MinorSpacingGridLength});
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = ViewConstants.Star1GridLength });
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.MinorSpacingGridLength});
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});
            var slider = new Slider
            {
                Minimum = MinimumValueProperty.Get(param.Metadata),
                Maximum = MaximumValueProperty.Get(param.Metadata),
                TickFrequency = TickFrequencyProperty.Get(param.Metadata),
                TickPlacement = TickPlacementProperty.Get(param.Metadata),
                AutoToolTipPlacement = AutoToolTipPlacement.BottomRight,
                IsSnapToTickEnabled = true,
            };
            var minimumTextBlock = new TextBlock { Text = $"{numberFormatter(slider.Minimum)}" };
            var maximumTextBlock = new TextBlock { Text = $"{numberFormatter(slider.Maximum)}" };
            Grid.SetColumn(minimumTextBlock, 0);
            Grid.SetColumn(slider, 2);
            Grid.SetColumn(maximumTextBlock, 4);
            grid.Children.Add(minimumTextBlock);
            grid.Children.Add(slider);
            grid.Children.Add(maximumTextBlock);

            slider.ValueChanged += (sender, e) => updateCallback();
            return new PresentedParameter(param, grid, new Accessor(param, slider), slider);
        }

    }

}