using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Media;

namespace SharpBCI.Extensions
{

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public static class ViewConstants
    {

        public const int MajorSpacing = 10;

        public const int MinorSpacing = 5;

        public const int DefaultRowHeight = 22;

        public const int SeparatorRowHeight = 15;

        public const int Intend = 20;

        public static readonly FontFamily FontConsolas = new FontFamily("Consolas");

        public static readonly Color SeparatorColor = Color.FromArgb(0xFF, 0xE4, 0xE4, 0xE4);

        public static readonly Color GroupHeaderColor = Color.FromScRgb(0.4F, 1, 1, 1);

        public static readonly Color InvalidColor = Color.FromScRgb(1, 1, 0.4F, 0.35F);

        public static readonly Thickness RowMargin = new Thickness {Top = 2, Bottom = 2, Left = 10, Right = 10};

    }

}
