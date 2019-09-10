using System;

namespace SharpBCI.Extensions.Data
{

    public enum Position1D 
    {
        Start = 0, Center = 1, End = 2
    }

    public enum PositionH1D
    {
        Left = 0, Center = 1, Right = 2
    }

    public enum PositionV1D
    {
        Top = 0, Middle = 1, Bottom = 2
    }

    public enum Position2D 
    {
        LeftTop = 0,
        CenterTop = 1,
        RightTop = 2,
        LeftMiddle = 3,
        CenterMiddle = 4,
        RightMiddle = 5,
        LeftBottom = 6,
        CenterBottom = 7,
        RightBottom = 8
    }

    public static class PositionExt
    {

        public static Position1D ToPosition1D(this PositionV1D position, bool inverted = false) => (Position1D) (inverted ? (2 - position) : position);

        public static Position1D ToPosition1D(this PositionH1D position, bool inverted = false) => (Position1D) (inverted ? (2 - position) : position);

        public static float GetPositionValue(this Position1D position)
        {
            switch (position)
            {
                case Position1D.Start:
                    return 0.0F;
                case Position1D.Center:
                    return 0.5F;
                case Position1D.End:
                    return 1.0F;
                default:
                    throw new ArgumentOutOfRangeException(nameof(position), position, null);
            }
        }

        public static PositionH1D GetHorizontalPosition(this Position2D position)
        {
            switch (position)
            {
                case Position2D.LeftTop:
                case Position2D.LeftMiddle:
                case Position2D.LeftBottom:
                    return PositionH1D.Left;
                case Position2D.CenterTop:
                case Position2D.CenterMiddle:
                case Position2D.CenterBottom:
                    return PositionH1D.Center;
                case Position2D.RightTop:
                case Position2D.RightMiddle:
                case Position2D.RightBottom:
                    return PositionH1D.Right;
                default:
                    throw new ArgumentOutOfRangeException(nameof(position), position, null);
            }
        }

        public static PositionV1D GetVerticalPosition(this Position2D position)
        {
            switch (position)
            {
                case Position2D.LeftTop:
                case Position2D.CenterTop:
                case Position2D.RightTop:
                    return PositionV1D.Top;
                case Position2D.LeftMiddle:
                case Position2D.CenterMiddle:
                case Position2D.RightMiddle:
                    return PositionV1D.Middle;
                case Position2D.LeftBottom:
                case Position2D.CenterBottom:
                case Position2D.RightBottom:
                    return PositionV1D.Bottom;
                default:
                    throw new ArgumentOutOfRangeException(nameof(position), position, null);
            }
        }

    }

}