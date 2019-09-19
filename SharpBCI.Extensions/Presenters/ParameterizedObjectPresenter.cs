using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MarukoLib.Lang;
using MarukoLib.Lang.Concurrent;
using SharpBCI.Extensions.Data;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    public class ParameterizedObjectPresenter : IPresenter
    {

        /// <summary>
        /// Default Value: true
        /// </summary>
        public static readonly NamedProperty<bool> ParamLabelVisibilityProperty = new NamedProperty<bool>("ParamLabelVisibility", true);
        
        /// <summary>
        /// Default Value: 1*
        /// </summary>
        public static readonly NamedProperty<GridLength> ColumnWidthProperty = new NamedProperty<GridLength>("ColumnWidth", new GridLength(1, GridUnitType.Star));

        public static readonly ParameterizedObjectPresenter Instance = new ParameterizedObjectPresenter();

        protected ParameterizedObjectPresenter() { }

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback) => 
            ParameterizedObjectExt.PopupProperty.Get(param.Metadata) ? PresentPopup(param, updateCallback) : PresentDirectly(param, updateCallback);

        [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
        public PresentedParameter PresentPopup(IParameterDescriptor param, Action updateCallback)
        {
            var factory = param.GetParameterizedObjectFactory();

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = new GridLength(4, GridUnitType.Star)});
            grid.ColumnDefinitions.Add(new ColumnDefinition {Width = new GridLength(1, GridUnitType.Star), MinWidth = 110, MaxWidth = 130});

            var button = new Button {Content = "Configure →" };
            grid.Children.Add(button);
            Grid.SetColumn(button, 1);

            var container = new Atomic<IParameterizedObject>();

            void Setter(object obj) => container.Set((IParameterizedObject) obj);

            object Getter() => container.Get();

            void Updater(ParameterStateType state, bool value)
            {
                if (state != ParameterStateType.Enabled) return;
                button.IsEnabled = value;
            }

            button.Click += (sender, e) =>
            {
                var subParams = factory.GetParameters(param);
                var context = factory.Parse(param, container.Get());
                var configWindow = new ParameterizedConfigWindow(param.Name ?? "Parameter", subParams, context) {Width = 400};
                if (configWindow.ShowDialog(out var @params))
                {
                    container.Set(factory.Create(param, @params));
                    updateCallback();
                }
            };

            return new PresentedParameter(param, grid, Getter, Setter, null, Updater);
        }

        public PresentedParameter PresentDirectly(IParameterDescriptor param, Action updateCallback)
        {
            var factory = param.GetParameterizedObjectFactory();
            var subParameters = factory.GetParameters(param).ToArray();
            var subParameterCount = subParameters.Length;
            var presentedSubParams = new PresentedParameter[subParameterCount];

            var grid = new Grid();
            if (subParameterCount == 0)
                grid.Children.Add(new TextBlock {Text = "<EMPTY>", Foreground = Brushes.DimGray});
            else
            {
                var labelVisible = ParamLabelVisibilityProperty.Get(param.Metadata);
                if (labelVisible) grid.RowDefinitions.Add(new RowDefinition {Height = GridLength.Auto});
                grid.RowDefinitions.Add(new RowDefinition {Height = GridLength.Auto});
                for (var i = 0; i < subParameterCount; i++)
                {
                    if (i != 0) grid.ColumnDefinitions.Add(new ColumnDefinition {Width = new GridLength(ViewConstants.MinorSpacing, GridUnitType.Pixel)});
                    grid.ColumnDefinitions.Add(new ColumnDefinition());
                }
                for (var i = 0; i < subParameterCount; i++)
                {
                    var columnIndex = i * 2;
                    var subParam = subParameters[i];
                    if (labelVisible)
                    {
                        var nameTextBlock = ViewHelper.CreateParamNameTextBlock(subParam);
                        nameTextBlock.FontSize = 8;
                        nameTextBlock.TextWrapping = TextWrapping.NoWrap;
                        nameTextBlock.TextAlignment = TextAlignment.Left;
                        grid.Children.Add(nameTextBlock);
                        Grid.SetRow(nameTextBlock, 0);
                        Grid.SetColumn(nameTextBlock, columnIndex);
                    }

                    var presentedSubParam = presentedSubParams[i] = subParam.GetPresenter().Present(subParam, updateCallback);
                    grid.Children.Add(presentedSubParam.Element);
                    if(labelVisible) Grid.SetRow(presentedSubParam.Element, 1);
                    Grid.SetColumn(presentedSubParam.Element, columnIndex);

                    GridLength columnWidth;
                    if (ColumnWidthProperty.TryGet(subParam.Metadata, out var propertyValue)) columnWidth = propertyValue;
                    else if (presentedSubParam.Element.GetType() == typeof(CheckBox)) columnWidth = GridLength.Auto;
                    else columnWidth = ColumnWidthProperty.DefaultValue;
                    grid.ColumnDefinitions[columnIndex].Width = columnWidth;
                }
            }

            void Setter(object obj)
            {
                var context = factory.Parse(param, (IParameterizedObject)obj);
                for (var i = 0; i < subParameters.Length; i++)
                    if (context.TryGet(subParameters[i], out var val)) presentedSubParams[i].Delegates.Setter(val);
            }

            object Getter()
            {
                var dict = new Context();
                for (var i = 0; i < subParameters.Length; i++) dict.Set(subParameters[i], presentedSubParams[i].Delegates.Getter());
                return factory.Create(param, dict);
            }

            bool Validator(object value)
            {
                var context = factory.Parse(param, (IParameterizedObject)value);
                var flag = true;
                for (var i = 0; i < subParameters.Length; i++)
                {
                    var subParam = subParameters[i];
                    bool valid;
                    try
                    {
                        var subValue = context.TryGet(subParam, out var val) ? val : subParam.DefaultValue;
                        valid = presentedSubParams[i].Delegates.Validator?.Invoke(subValue) ?? true;
                    }
                    catch (Exception)
                    {
                        valid = false;
                    }
                    presentedSubParams[i].Delegates.Updater?.Invoke(ParameterStateType.Valid, valid);
                    if (!valid) flag = false;
                }
                return flag;
            }

            void Updater(ParameterStateType state, bool value)
            {
                if (state != ParameterStateType.Enabled) return;
                if (value)
                {
                    var context = factory.Parse(param, (IParameterizedObject)Getter());
                    for (var i = 0; i < subParameters.Length; i++)
                        presentedSubParams[i].Delegates.Updater?.Invoke(ParameterStateType.Enabled, factory.IsEnabled(context, subParameters[i]));
                }
                else
                    foreach (var presentedSubParam in presentedSubParams)
                        presentedSubParam.Delegates.Updater?.Invoke(ParameterStateType.Enabled, false);
            }

            return new PresentedParameter(param, grid, Getter, Setter, Validator, Updater);
        }
    }
}