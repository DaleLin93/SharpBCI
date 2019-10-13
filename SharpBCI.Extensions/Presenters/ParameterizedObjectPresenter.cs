using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarukoLib.Lang;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    public class ParameterizedObjectPresenter : IPresenter
    {

        private class PopupAccessor : IPresentedParameterAccessor
        {

            internal IParameterizedObject Value;

            private readonly IParameterDescriptor _parameter;

            public PopupAccessor(IParameterDescriptor parameter, IParameterizedObject value)
            {
                _parameter = parameter;
                Value = value;
            }

            public object GetValue() => _parameter.IsValid(Value);

            public void SetValue(object value) => Value = (IParameterizedObject)value;

        }

        private class InlineAdapter : IPresentedParameterAdapter
        {

            private readonly IParameterDescriptor _parameter;

            private readonly IParameterizedObjectFactory _factory;

            private readonly PresentedParameter[] _subParameters;

            public InlineAdapter(IParameterDescriptor parameter, IParameterizedObjectFactory factory, PresentedParameter[] subParameters)
            {
                _parameter = parameter;
                _factory = factory;
                _subParameters = subParameters;
            }

            public object GetValue()
            {
                var context = new Context();
                var errors = new LinkedList<PresentedParameter>();
                foreach (var subParam in _subParameters)
                    try
                    {
                        context.Set(subParam.ParameterDescriptor, subParam.GetValue());
                    }
                    catch (Exception)
                    {
                        subParam.SetValid(false);
                        errors.AddLast(subParam);
                    }
                if (errors.Any()) throw new Exception();
                return _factory.Create(_parameter, context);
            }

            public void SetValue(object value)
            {
                var context = _factory.Parse(_parameter, (IParameterizedObject)value);
                foreach (var subParam in _subParameters)
                    if (context.TryGet(subParam.ParameterDescriptor, out var val)) subParam.SetValue(val);
            }

            public void SetEnabled(bool value)
            {
                if (value)
                {
                    var context = _factory.Parse(_parameter, (IParameterizedObject)GetValue());
                    foreach (var presentedParam in _subParameters)
                        presentedParam.SetEnabled(_factory.IsEnabled(context, presentedParam.ParameterDescriptor));
                }
                else
                    foreach (var presentedSubParam in _subParameters)
                        presentedSubParam.SetEnabled(false);
            }

            public void SetValid(bool value) { }

        }

        /// <summary>
        /// Default Value: Horizontal
        /// </summary>
        public static readonly NamedProperty<Orientation> LayoutOrientationVisibilityProperty = new NamedProperty<Orientation>("LayoutOrientation", Orientation.Horizontal);

        /// <summary>
        /// Default Value: true
        /// </summary>
        public static readonly NamedProperty<bool> ParamLabelVisibilityProperty = new NamedProperty<bool>("ParamLabelVisibility", true);

        /// <summary>
        /// Default Value: 1*
        /// </summary>
        public static readonly NamedProperty<GridLength> ColumnWidthProperty = new NamedProperty<GridLength>("ColumnWidth", ViewConstants.Star1GridLength);

        public static readonly ParameterizedObjectPresenter Instance = new ParameterizedObjectPresenter();

        protected ParameterizedObjectPresenter() { }

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback) => 
            ParameterizedObjectExt.PopupProperty.Get(param.Metadata) ? PresentPopup(param, updateCallback) : PresentInline(param, updateCallback);

        public PresentedParameter PresentPopup(IParameterDescriptor param, Action updateCallback)
        {
            var factory = param.GetParameterizedObjectFactory();

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = new GridLength(4, GridUnitType.Star)});
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.Star1GridLength, MinWidth = 110, MaxWidth = 130});

            var button = new Button {Content = "Configure →" };
            grid.Children.Add(button);
            Grid.SetColumn(button, 1);

            var accessor = new PopupAccessor(param, null);

            button.Click += (sender, e) =>
            {
                var subParams = factory.GetParameters(param);
                var context = factory.Parse(param, accessor.Value);
                var configWindow = new ParameterizedConfigWindow(param.Name ?? "Parameter", subParams, context) {Width = 400};
                if (!configWindow.ShowDialog(out var @params)) return;
                accessor.Value = factory.Create(param, @params);
                updateCallback();
            };

            return new PresentedParameter(param, grid, accessor, button);
        }

        public PresentedParameter PresentInline(IParameterDescriptor param, Action updateCallback)
        {
            var factory = param.GetParameterizedObjectFactory();
            var subParameters = factory.GetParameters(param).ToArray();
            var subParameterCount = subParameters.Length;
            var presentedSubParams = new PresentedParameter[subParameterCount];

            UIElement container;
            if (subParameterCount == 0)
            {
                var grid = new Grid();
                grid.Children.Add(new TextBlock { Text = "<EMPTY>", Foreground = Brushes.DimGray });
                container = grid;
            }
            else
            {
                var labelVisible = ParamLabelVisibilityProperty.Get(param.Metadata);
                var nameTextBlocks = labelVisible ? new TextBlock[subParameterCount] : null;
                for (var i = 0; i < subParameterCount; i++)
                {
                    var subParam = subParameters[i];
                    presentedSubParams[i] = subParam.GetPresenter().Present(subParam, updateCallback);
                    if (!labelVisible) continue;
                    var nameTextBlock = ViewHelper.CreateParamNameTextBlock(subParam);
                    nameTextBlock.FontSize = 8;
                    nameTextBlock.TextWrapping = TextWrapping.NoWrap;
                    nameTextBlock.TextAlignment = TextAlignment.Left;
                    nameTextBlocks[i] = nameTextBlock;
                }

                var orientation = LayoutOrientationVisibilityProperty.Get(param.Metadata);
                switch (orientation)
                {
                    case Orientation.Horizontal:
                    {
                        var grid = new Grid();
                        if (labelVisible) grid.RowDefinitions.Add(new RowDefinition {Height = GridLength.Auto});
                        grid.RowDefinitions.Add(new RowDefinition {Height = GridLength.Auto});
                        grid.ColumnDefinitions.Add(new ColumnDefinition()); // first content column (at least one element is ensured).
                        for (var i = 1; i < subParameterCount; i++)
                        {
                            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = ViewConstants.MinorSpacingGridLength});
                            grid.ColumnDefinitions.Add(new ColumnDefinition());
                        }
                        for (var i = 0; i < subParameterCount; i++)
                        {
                            var columnIndex = i * 2;
                            var subParam = subParameters[i];
                            var presentedSubParam = presentedSubParams[i];
                            if (labelVisible)
                            {
                                var nameTextBlock = nameTextBlocks[i];
                                grid.Children.Add(nameTextBlock);
                                Grid.SetRow(nameTextBlock, 0);
                                Grid.SetColumn(nameTextBlock, columnIndex);
                            }

                            grid.Children.Add(presentedSubParam.Element);
                            if (labelVisible) Grid.SetRow(presentedSubParam.Element, 1);
                            Grid.SetColumn(presentedSubParam.Element, columnIndex);

                            GridLength columnWidth;
                            if (ColumnWidthProperty.TryGet(subParam.Metadata, out var propertyValue))
                                columnWidth = propertyValue;
                            else if (presentedSubParam.Element.GetType() == typeof(CheckBox))
                                columnWidth = GridLength.Auto;
                            else columnWidth = ColumnWidthProperty.DefaultValue;
                            grid.ColumnDefinitions[columnIndex].Width = columnWidth;
                        }
                        container = grid;
                        break;
                    }
                    case Orientation.Vertical:
                    {
                        var stackPanel = new StackPanel();
                        for (var i = 0; i < subParameterCount; i++)
                        {
                            var presentedSubParam = presentedSubParams[i];
                            if (labelVisible)
                            {
                                var nameTextBlock = nameTextBlocks[i];
                                stackPanel.Children.Add(nameTextBlock);
                            }
                            stackPanel.Children.Add(presentedSubParam.Element);
                        }
                        container = stackPanel;
                        break;
                    }
                    default:
                        throw new NotSupportedException(orientation.ToString());
                }
            }
            return new PresentedParameter(param, container, new InlineAdapter(param, factory, presentedSubParams));
        }
    }
}