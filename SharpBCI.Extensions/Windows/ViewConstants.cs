using System;
using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SharpBCI.Extensions.Windows
{

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class ViewConstants
    {

        public const int MajorSpacing = 10;

        public const int MinorSpacing = 5;

        public const int DefaultRowHeight = 22;

        public const int Intend = 15;

        public const string NotSelectedComboBoxItemText = "<NOT SELECTED>";

        public const string SharedResourceDictionaryUri = "pack://application:,,,/SharpBCI.Extensions;component/Resources/SharedResourceDictionary.xaml";

        public const string AlertImageUri = "pack://application:,,,/SharpBCI.Extensions;component/Resources/Alert.png";

        public const string MinusGrayImageUri = "pack://application:,,,/SharpBCI.Extensions;component/Resources/MinusGray.png";

        public const string MinusRedImageUri = "pack://application:,,,/SharpBCI.Extensions;component/Resources/MinusRed.png";

        public const string PreviewImageUri = "pack://application:,,,/SharpBCI.Extensions;component/Resources/Preview.png";

        public const string ConfigImageUri = "pack://application:,,,/SharpBCI.Extensions;component/Resources/Config.png";

        public const string ResetImageUri = "pack://application:,,,/SharpBCI.Extensions;component/Resources/Reset.png";

        public static readonly Duration DefaultAnimationDuration = new Duration(TimeSpan.FromMilliseconds(300));

        public static readonly IEasingFunction DefaultEasingFunction = new QuadraticEase();

        public static readonly GridLength Star1GridLength = new GridLength(1, GridUnitType.Star);

        public static readonly GridLength MajorSpacingGridLength = new GridLength(MajorSpacing, GridUnitType.Pixel);

        public static readonly GridLength MinorSpacingGridLength = new GridLength(MinorSpacing, GridUnitType.Pixel);

        public static readonly FontFamily FontConsolas = new FontFamily("Consolas");
        
        public static readonly Color InvalidColor = Color.FromScRgb(1, 1, 0.4F, 0.35F);

        public static readonly Brush InvalidColorBrush = new SolidColorBrush(InvalidColor);

        public static readonly Thickness RowMargin = new Thickness {Top = 1, Bottom = 1, Left = 5, Right = 10};

    }

}
