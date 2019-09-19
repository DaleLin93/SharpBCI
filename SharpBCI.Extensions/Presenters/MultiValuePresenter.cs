using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Presenters
{

    public class MultiValuePresenter : IPresenter
    {

        private sealed class ArrayElementParameter : RoutedParameter
        {

            internal ArrayElementParameter(IParameterDescriptor originalParameter) : base(originalParameter) => ElementType = originalParameter.ValueType.GetElementType();

            public Type ElementType { get; }

            public override string Key => OriginalParameter.Key;

            public override string Name => OriginalParameter.Name;

            public override string Unit => OriginalParameter.Unit;

            public override string Description => OriginalParameter.Description;

            public override Type ValueType => ElementType;

            public override bool IsNullable => ElementType.IsNullableType();

            public override object DefaultValue => Activator.CreateInstance(ElementType);

            public override IEnumerable SelectableValues => EmptyArray<object>.Instance;

            public override IReadonlyContext Metadata => OriginalParameter.Metadata;

            public override bool IsValid(object value) => OriginalParameter.IsValid(value);

        }

        public static readonly NamedProperty<int> MaximumElementCountProperty = new NamedProperty<int>("MaximumElementCount", int.MaxValue);

        public static readonly MultiValuePresenter Instance = new MultiValuePresenter();

        [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var maximumElementCount = MaximumElementCountProperty.Get(param.Metadata);
            var elementType = param.ValueType.GetElementType() ?? throw new ArgumentException("array type required");
            if (elementType.IsPrimitive) return PlainTextPresenter.Instance.Present(param, updateCallback);

            var parameterList = new List<Tuple<PresentedParameter, Grid>>();
            var elementTypePresenter = elementType.GetPresenter();
            var elementParameter = new ArrayElementParameter(param);
            var stackPanel = new StackPanel();
            var listPanel = new StackPanel();
            var plusButton = new Button { Content = "+" };

            stackPanel.Children.Add(listPanel);
            stackPanel.Children.Add(plusButton);

            void UpdateButtonState() => plusButton.IsEnabled = parameterList.Count < maximumElementCount;

            void AddRow()
            {
                if (parameterList.Count >= maximumElementCount) return;
                var presentedParameter = elementTypePresenter.Present(elementParameter, updateCallback);

                var grid = new Grid {Margin = new Thickness {Top = 2, Bottom = 2}};
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(5, GridUnitType.Pixel) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(0.1, GridUnitType.Star), MinWidth = 12, MaxWidth = 20});

                grid.Children.Add(presentedParameter.Element);
                Grid.SetColumn(presentedParameter.Element, 0);

                var minusButton = new Button {Content = "-", Margin = new Thickness {Top = 2}};
                grid.Children.Add(minusButton);
                Grid.SetColumn(minusButton, 2);

                var tuple = new Tuple<PresentedParameter, Grid>(presentedParameter, grid);

                parameterList.Add(tuple);
                listPanel.Children.Add(grid);

                minusButton.Click += (s1, e1) =>
                {
                    parameterList.Remove(tuple);
                    listPanel.Children.Remove(grid);
                    UpdateButtonState();
                };
                UpdateButtonState();
            };
            void RemoveLastRow()
            {
                var index = parameterList.Count - 1;
                var tuple = parameterList[index];
                parameterList.Remove(tuple);
                listPanel.Children.Remove(tuple.Item2);
                UpdateButtonState();
            };

            object Getter()
            {
                var array = Array.CreateInstance(elementType, parameterList.Count);
                for (var i = 0; i < parameterList.Count && i < maximumElementCount; i++)
                    array.SetValue(parameterList[i].Item1.Delegates.Getter(), i);
                return array;
            }
            void Setter(object arrayObj)
            {
                if (arrayObj is Array array)
                {
                    while (parameterList.Count < array.Length && parameterList.Count < maximumElementCount) AddRow();
                    while (parameterList.Count > array.Length || parameterList.Count > maximumElementCount) RemoveLastRow();
                    for (var i = 0; i < array.Length && i < maximumElementCount; i++)
                        parameterList[i].Item1.Delegates.Setter(array.GetValue(i));
                }
            }
            void Updater(ParameterStateType state, bool value)
            {
                // TODO
            }

            plusButton.Click += (s0, e0) => AddRow();

            return new PresentedParameter(param, stackPanel, new PresentedParameter.ParamDelegates(Getter, Setter, param.IsValid, Updater));
        }

    }

}