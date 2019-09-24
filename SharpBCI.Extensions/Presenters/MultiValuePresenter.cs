using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    public class MultiValuePresenter : IPresenter
    {

        private class Adapter : IPresentedParameterAdapter
        {

            private readonly IParameterDescriptor _parameter;

            private readonly Type _elementType;

            private readonly bool _isDistinct, _isFixed;

            private readonly int _maxElementCount;

            private readonly UIElement _container;

            private readonly List<Tuple<PresentedParameter, UIElement>> _list;

            private readonly Action _addRow, _removeLastRow;

            public Adapter(IParameterDescriptor parameter, Type elementType, bool isDistinct, bool isFixed, int maxElementCount,
                UIElement container, List<Tuple<PresentedParameter, UIElement>> list, Action addRow, Action removeLastRow)
            {
                _parameter = parameter;
                _elementType = elementType;
                _isDistinct = isDistinct;
                _isFixed = isFixed;
                _maxElementCount = maxElementCount;
                _container = container;
                _list = list;
                _addRow = addRow;
                _removeLastRow = removeLastRow;
            }

            public object GetValue()
            {
                IList result = Array.CreateInstance(_elementType, _list.Count);
                for (var i = 0; i < _list.Count && i < _maxElementCount; i++)
                    result[i] = _list[i].Item1.GetValue();
                if (_isDistinct)
                    for (var i = 1; i < result.Count; i++)
                    {
                        var primary = result[i];
                        for (var j = 0; j < i; j++)
                            if (Equals(primary, result[i]))
                                throw new Exception("duplicated value");
                    }
                return _parameter.IsValidOrThrow(result);
            }

            public void SetValue(object value)
            {
                if (value is IList list)
                {
                    if (!_isFixed)
                    {
                        while (_list.Count < list.Count && _list.Count < _maxElementCount) _addRow();
                        while (_list.Count > list.Count || _list.Count > _maxElementCount) _removeLastRow();
                    }
                    for (var i = 0; i < list.Count && i < _list.Count; i++)
                        _list[i].Item1.SetValue(list[i]);
                }
            }

            public void SetEnabled(bool value) => _container.IsEnabled = value;

            public void SetValid(bool value) { }

        }

        internal sealed class ArrayElementParameter : RoutedParameter
        {

            internal ArrayElementParameter([NotNull] IParameterDescriptor originalParameter,
                [NotNull] Type elementType, [NotNull] IReadonlyContext metadata) : base(originalParameter)
            {
                ElementType = elementType;
                Metadata = metadata;
            }

            public Type ElementType { get; }

            public override string Key => OriginalParameter.Key;

            public override string Name => OriginalParameter.Name;

            public override string Unit => OriginalParameter.Unit;

            public override string Description => OriginalParameter.Description;

            public override Type ValueType => ElementType;

            public override bool IsNullable => ElementType.IsNullableType();

            public override object DefaultValue => Activator.CreateInstance(ElementType);

            public override IEnumerable SelectableValues => EmptyArray<object>.Instance;

            public override IReadonlyContext Metadata { get; }

            public override bool IsValid(object value) => true;

        }

        private class MinusButton : Grid
        {

            public MinusButton(int width, int height, Action clickAction)
            {
                Rectangle backgroundRect;
                Children.Add(backgroundRect = new Rectangle
                {
                    Fill = Brushes.DarkGray,
                    Width = width,
                    Height = height,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                });
                backgroundRect.RadiusX = backgroundRect.RadiusY = Math.Min(width, height) / 2.0;
                Children.Add(new Rectangle
                {
                    Fill = Brushes.White,
                    Width = Math.Min(width, height) * 2 / 3.0,
                    Height = Math.Min(width, height) * 2 / 9.0,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    IsHitTestVisible = false
                });
                backgroundRect.MouseEnter += (s0, e0) => ((Rectangle)s0).Fill = Brushes.IndianRed;
                backgroundRect.MouseLeave += (s0, e0) => ((Rectangle)s0).Fill = Brushes.DarkGray;
                backgroundRect.MouseUp += (s1, e1) => clickAction();
            }

        }

        private class PlusButton : Grid
        {

            public PlusButton(int height, Action clickAction)
            {
                Rectangle backgroundRect;
                Children.Add(backgroundRect = new Rectangle
                {
                    Fill = Brushes.DimGray,
                    StrokeThickness = 1,
                    Height = height,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                });
                backgroundRect.RadiusX = backgroundRect.RadiusY = height / 2.0;
                Children.Add(new Rectangle
                {
                    Fill = Brushes.White,
                    Width = height * 2 / 3.0,
                    Height = height * 2 / 9.0,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    IsHitTestVisible = false
                });
                Children.Add(new Rectangle
                {
                    Fill = Brushes.White,
                    Width = height * 2 / 9.0,
                    Height = height * 2 / 3.0,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    IsHitTestVisible = false
                });
                backgroundRect.MouseEnter += (s0, e0) => ((Rectangle)s0).Fill = Brushes.MediumAquamarine;
                backgroundRect.MouseLeave += (s0, e0) => ((Rectangle)s0).Fill = Brushes.DimGray;
                backgroundRect.MouseUp += (s1, e1) => clickAction();
            }

        }

        public static readonly NamedProperty<bool> DistinctProperty = new NamedProperty<bool>("Distinct", false);

        public static readonly NamedProperty<int> FixedElementCountProperty = new NamedProperty<int>("FixedElementCount", -1);

        public static readonly NamedProperty<int> MaximumElementCountProperty = new NamedProperty<int>("MaximumElementCount", int.MaxValue);

        public static readonly NamedProperty<IReadonlyContext> ElementContextProperty = new NamedProperty<IReadonlyContext>("ElementContext", EmptyContext.Instance);

        public static readonly MultiValuePresenter Instance = new MultiValuePresenter();

        private static Type GetElementType(Type type)
        {
            if (type.IsArray) return type.GetElementType();
            if (typeof(IList<>).IsAssignableFrom(type)) return type.GetGenericType(typeof(IList<>)) ?? throw new ArgumentException("Generic type not defined");
            throw new ArgumentException("Array or IList<> type required");
        }

        [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
        [SuppressMessage("ReSharper", "AccessToModifiedClosure")]
        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var isDistinct = DistinctProperty.Get(param.Metadata);
            var fixedCount = FixedElementCountProperty.Get(param.Metadata);
            var isFixed = fixedCount >= 0;
            var maximumElementCount = MaximumElementCountProperty.Get(param.Metadata);
            var elementType = GetElementType(param.ValueType);
            if (elementType.IsPrimitive) return PlainTextPresenter.Instance.Present(param, updateCallback);

            var elementList = new List<Tuple<PresentedParameter, UIElement>>();
            var elementTypePresenter = elementType.GetPresenter();
            var elementParameter = new ArrayElementParameter(param, elementType, ElementContextProperty.Get(param.Metadata));

            /* Outer grid container, with rounded rect background */
            var container = new Grid {Margin = new Thickness(0, 3, 0, 3)};
            container.Children.Add(new Rectangle
            {
                RadiusX = 6, RadiusY = 6,
                Stroke = Brushes.SlateGray,
                StrokeDashArray = new DoubleCollection(new []{3.0, 3.0}),
                StrokeThickness = 1,
            });

            /* Middle stack container, with element stack panel and plus button */
            var stackPanel = new StackPanel{Margin = new Thickness(7)};
            container.Children.Add(stackPanel);

            /* Inner stack container, with elements */
            StackPanel listPanel;
            if (!isFixed) stackPanel.Children.Add(listPanel = new StackPanel());
            else listPanel = stackPanel;

            Action updateButtonState = null;

            void AddRow()
            {
                if (elementList.Count >= maximumElementCount) return;
                var presentedParameter = elementTypePresenter.Present(elementParameter, updateCallback);

                if (!isFixed)
                {
                    /* Row grid container, with actual presented parameter and minus button */
                    var grid = new Grid {Margin = new Thickness {Top = 2, Bottom = 2}};
                    var tuple = new Tuple<PresentedParameter, UIElement>(presentedParameter, grid);

                    grid.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.Star1GridLength});
                    grid.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.MinorSpacingGridLength});
                    grid.ColumnDefinitions.Add(new ColumnDefinition {Width = GridLength.Auto});

                    grid.Children.Add(presentedParameter.Element);
                    Grid.SetColumn(presentedParameter.Element, 0);
                    
                    var minusButton = new MinusButton(15, 15, () =>
                    {
                        elementList.Remove(tuple);
                        listPanel.Children.Remove(grid);
                        updateButtonState?.Invoke();
                    });
                    grid.Children.Add(minusButton);
                    Grid.SetColumn(minusButton, 2);

                    elementList.Add(tuple);
                    listPanel.Children.Add(grid);

                    updateButtonState?.Invoke();
                }
                else
                {
                    var tuple = new Tuple<PresentedParameter, UIElement>(presentedParameter, presentedParameter.Element);
                    listPanel.Children.Add(presentedParameter.Element);
                    elementList.Add(tuple);
                }
            }
            void RemoveLastRow()
            {
                var index = elementList.Count - 1;
                var tuple = elementList[index];
                elementList.Remove(tuple);
                listPanel.Children.Remove(tuple.Item2);
                updateButtonState?.Invoke();
            }
            if (!isFixed)
            {
                var plusButton = new PlusButton(15, AddRow) {Margin = new Thickness(0, ViewConstants.MinorSpacing, 0, 0)};
                stackPanel.Children.Add(plusButton);
                updateButtonState = () => plusButton.IsEnabled = elementList.Count < maximumElementCount;
            }
            else
            {
                while (elementList.Count < fixedCount) AddRow();
            }
            return new PresentedParameter(param, container, new Adapter(param, elementType, isDistinct, isFixed, 
                maximumElementCount, container, elementList, AddRow, RemoveLastRow));
        }

    }

}