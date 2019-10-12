using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using MarukoLib.Lang;
using MarukoLib.UI;

namespace SharpBCI.Extensions.Presenters
{

    public class MarkerDefinitionPresenter : IPresenter
    {

        private class ComboBoxAccessor : IPresentedParameterAccessor
        {

            private readonly IParameterDescriptor _parameter;

            private readonly Func<object, string> _toStringFunc;

            private readonly ComboBox _comboBox;

            public ComboBoxAccessor(IParameterDescriptor parameter, Func<object, string> toStringFunc, ComboBox comboBox)
            {
                _parameter = parameter;
                _toStringFunc = toStringFunc;
                _comboBox = comboBox;
            }

            public object GetValue()
            {
                var value = ToStringOverridenWrapper.TryUnwrap(_comboBox.SelectedValue);
                if (ReferenceEquals(NullValue, value)) value = null;
                return _parameter.IsValidOrThrow(value);
            }

            public void SetValue(object value) => _comboBox.FindAndSelect(_toStringFunc(value), 0);

        }

        private static readonly string NullValue = "<NULL>";

        public static readonly NamedProperty<IEnumerable<MarkerDefinition>> MarkerDefinitionsProperty = new NamedProperty<IEnumerable<MarkerDefinition>>("MarkerDefinitions");

        public static readonly NamedProperty<string> MarkerPrefixFilterProperty = new NamedProperty<string>("MarkerPrefixFilter");

        public static readonly NamedProperty<Regex> MarkerRegexFilterProperty = new NamedProperty<Regex>("MarkerRegexFilter");

        public static readonly MarkerDefinitionPresenter Instance = new MarkerDefinitionPresenter();

        public static ContextProperty<Func<IParameterDescriptor, IEnumerable>> SelectableValuesFuncProperty = 
            new ContextProperty<Func<IParameterDescriptor, IEnumerable>>();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var allowsNull = param.IsNullable;
            var markerDefinitions = MarkerDefinitionsProperty.TryGet(param.Metadata, out var propValue) 
                ? propValue : MarkerDefinitions.MarkerRegistry.Registered;
            if (MarkerPrefixFilterProperty.TryGet(param.Metadata, out var markerPrefixFilter) && !string.IsNullOrWhiteSpace(markerPrefixFilter))
                markerDefinitions = markerDefinitions.Where(md => md.Name.StartsWith(markerPrefixFilter));
            if (MarkerRegexFilterProperty.TryGet(param.Metadata, out var markerRegexFilter) && markerRegexFilter != null)
                markerDefinitions = markerDefinitions.Where(md => markerRegexFilter.IsMatch(md.Name));
            var selectableValues = allowsNull ? new object[] { NullValue }.Concat(markerDefinitions.Cast<object>()) : (IEnumerable)markerDefinitions;
            static string ToStringFunc(object obj)
            {
                if (obj == null) return NullValue;
                if (obj is MarkerDefinition markerDefinition) return $"{markerDefinition.Name} (#{markerDefinition.Code})";
                return obj?.ToString();
            }
            var comboBox = new ComboBox();
            comboBox.SelectionChanged += (sender, args) => updateCallback();
            comboBox.ItemsSource = ToStringOverridenWrapper.Of(selectableValues, ToStringFunc);
            return new PresentedParameter(param, comboBox, new ComboBoxAccessor(param, ToStringFunc, comboBox), comboBox);
        }

    }
}