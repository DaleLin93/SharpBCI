using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MarukoLib.UI;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{
    public class ColorPresenter : IPresenter
    {

        public static readonly ColorPresenter Instance = new ColorPresenter();

        [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var isSdColor = param.ValueType == typeof(System.Drawing.Color) || param.ValueType == typeof(System.Drawing.Color?);
            var grid = new Grid {MinHeight = 15};
            var rectangle = new Rectangle {Stroke = (Brush) ViewHelper.GetResource("SeparatorColorBrush")};
            var textBlock = new TextBlock
            {
                FontSize = 8,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,  
                Visibility = Visibility.Hidden,
                IsHitTestVisible = false,
            };
            grid.Children.Add(rectangle);
            grid.Children.Add(textBlock);
            rectangle.MouseEnter += (sender, e) => textBlock.Visibility = Visibility.Visible;
            rectangle.MouseLeave += (sender, e) => textBlock.Visibility = Visibility.Hidden;
            rectangle.MouseLeftButtonUp += (sender, e) =>
            {
                var dialog = new System.Windows.Forms.ColorDialog {Color = ((rectangle.Fill as SolidColorBrush)?.Color ?? Colors.Red).ToSdColor()};
                if (System.Windows.Forms.DialogResult.OK != dialog.ShowDialog())
                    return;
                rectangle.Fill = new SolidColorBrush(dialog.Color.ToSwmColor());
                updateCallback();
            };
            void Setter(object input)
            {
                var color = (isSdColor ? ((System.Drawing.Color?)input)?.ToSwmColor() : (Color?)input) ?? Color.FromScRgb(0, 0, 0, 0);
                rectangle.Fill = new SolidColorBrush(color);
                textBlock.Text = $"ARGB({color.A}, {color.R}, {color.G}, {color.B})";
                textBlock.Foreground = new SolidColorBrush(color.Inverted());
            };
            object Getter()
            {
                var color = ((SolidColorBrush)rectangle.Fill).Color;
                return isSdColor ? color.ToSdColor() : (object)color;
            };
            void Updater(ParameterStateType state, bool value)
            {
                if (state == ParameterStateType.Enabled) rectangle.IsEnabled = value;
            }
            return new PresentedParameter(param, grid, new PresentedParameter.ParamDelegates(Getter, Setter, param.IsValid, Updater));
        }

    }
}