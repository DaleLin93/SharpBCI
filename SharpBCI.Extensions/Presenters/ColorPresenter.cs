using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using MarukoLib.Lang;
using MarukoLib.UI;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Presenters
{

    public class ColorPresenter : IPresenter
    {

        private class Adapter : IPresentedParameterAdapter
        {

            private readonly IParameterDescriptor _parameter;

            private readonly bool _isSdColor;

            private readonly Rectangle _rect;

            private readonly TextBlock _textBlock;

            public Adapter(IParameterDescriptor parameter, bool isSdColor, Rectangle rect, TextBlock textBlock)
            {
                _parameter = parameter;
                _isSdColor = isSdColor;
                _rect = rect;
                _textBlock = textBlock;
            }

            public object GetValue()
            {
                var color = ((SolidColorBrush)_rect.Fill).Color;
                return _parameter.IsValidOrThrow(_isSdColor ? color.ToSdColor() : (object) color);
            }

            public void SetValue(object value)
            {
                var color = (_isSdColor ? ((System.Drawing.Color?)value)?.ToSwmColor() : (Color?)value) ?? Color.FromScRgb(0, 0, 0, 0);
                _rect.Fill = new SolidColorBrush(color);
                if (_textBlock != null)
                {
                    _textBlock.Text = $"ARGB({color.A}, {color.R}, {color.G}, {color.B})";
                    _textBlock.Foreground = new SolidColorBrush(color.Inverted());
                }
            }

            public void SetEnabled(bool value) => _rect.IsEnabled = value;

            public void SetValid(bool value) { }

        }

        /// <summary>
        /// Default Value: true
        /// </summary>
        public static readonly NamedProperty<bool> ShowArgbProperty = new NamedProperty<bool>("ShowArgb", true);

        public static readonly ColorPresenter Instance = new ColorPresenter();

        public PresentedParameter Present(IParameterDescriptor param, Action updateCallback)
        {
            var isSdColor = param.ValueType == typeof(System.Drawing.Color) || param.ValueType == typeof(System.Drawing.Color?);
            var rectangle = new Rectangle {Stroke = (Brush) ViewHelper.GetResource("SeparatorFillBrush"), MinHeight = 15};
            Grid grid = null;
            TextBlock textBlock = null;
            if (ShowArgbProperty.Get(param.Metadata))
            {
                grid = new Grid();
                grid.Children.Add(rectangle);
                textBlock = new TextBlock
                {
                    FontSize = 8,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Visibility = Visibility.Hidden,
                    IsHitTestVisible = false,
                };
                grid.Children.Add(textBlock);
                rectangle.MouseEnter += (sender, e) => textBlock.Visibility = Visibility.Visible;
                rectangle.MouseLeave += (sender, e) => textBlock.Visibility = Visibility.Hidden;
            }
            rectangle.MouseLeftButtonUp += (sender, e) =>
            {
                var dialog = new System.Windows.Forms.ColorDialog {Color = ((rectangle.Fill as SolidColorBrush)?.Color ?? Colors.Red).ToSdColor()};
                if (System.Windows.Forms.DialogResult.OK != dialog.ShowDialog())
                    return;
                rectangle.Fill = new SolidColorBrush(dialog.Color.ToSwmColor());
                updateCallback();
            };
            return new PresentedParameter(param, grid ?? (UIElement) rectangle, new Adapter(param, isSdColor, rectangle, textBlock));
        }

    }

}