using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;
using MarukoLib.UI;

namespace SharpBCI.Extensions.Presenters
{
    public class ColorPresenter : IPresenter
    {

        public static readonly ColorPresenter Instance = new ColorPresenter();

        [SuppressMessage("ReSharper", "ImplicitlyCapturedClosure")]
        public PresentedParameter Present(Window window, IParameterDescriptor param, Action updateCallback)
        {
            var isSdColor = param.ValueType == typeof(System.Drawing.Color) || param.ValueType == typeof(System.Drawing.Color?);
            var rectangle = new Rectangle { Stroke = new SolidColorBrush(ViewConstants.SeparatorColor), MinHeight = 15 };
            rectangle.MouseLeftButtonUp += (sender, e) =>
            {
                var dialog = new System.Windows.Forms.ColorDialog { Color = ((rectangle.Fill as SolidColorBrush)?.Color ?? Colors.Red).ToSdColor() };
                if (System.Windows.Forms.DialogResult.OK != dialog.ShowDialog())
                    return;
                rectangle.Fill = new SolidColorBrush(dialog.Color.ToSwmColor());
                updateCallback();
            };
            void Setter(object color)
            {
                var converted = isSdColor
                    ? ((System.Drawing.Color?)color ?? System.Drawing.Color.FromArgb(0)).ToSwmColor()
                    : (Color?)color ?? Color.FromScRgb(0, 0, 0, 0);
                rectangle.Fill = new SolidColorBrush(converted);
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
            return new PresentedParameter(param, rectangle, new PresentedParameter.ParamDelegates(Getter, Setter, param.IsValid, Updater));
        }

    }
}