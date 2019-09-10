using System.Windows;
using MarukoLib.Lang;
using MarukoLib.UI;
using Newtonsoft.Json;

namespace SharpBCI.Core.Shared
{

    public class ScreenParams : IDescribable
    {

        public readonly int Index;

        public readonly string DeviceName;

        public readonly int BitsPerPixel;

        public readonly bool Primary;

        public readonly double X, Y, Width, Height;

        public readonly double ScaleFactor;

        [JsonConstructor]
        private ScreenParams([JsonProperty("Index")]int index, [JsonProperty("DeviceName")]string deviceName,
            [JsonProperty("BitsPerPixel")]int bitsPerPixel, [JsonProperty("Primary")]bool primary,
            [JsonProperty("X")]double x, [JsonProperty("Y")]double y, 
            [JsonProperty("Width")]double width, [JsonProperty("Height")]double height,
            [JsonProperty("ScaleFactor")]double scaleFactor)
        {
            Index = index;
            DeviceName = deviceName;
            BitsPerPixel = bitsPerPixel;
            Primary = primary;
            X = x;
            Y = y;
            Width = width;
            Height = height;
            ScaleFactor = scaleFactor;
        }

        public static ScreenParams[] All
        {
            get
            {
                var screens = System.Windows.Forms.Screen.AllScreens;
                var screenParamsArray = new ScreenParams[screens.Length];
                var scaleFactor = GraphicsUtils.Scale;
                for (var i = 0; i < screens.Length; i++)
                {
                    var screen = screens[i];
                    var bounds = screen.Bounds;
                    screenParamsArray[i] = new ScreenParams
                    (
                        index: i,
                        deviceName: screen.DeviceName,
                        bitsPerPixel: screen.BitsPerPixel,
                        primary: screen.Primary,
                        x: bounds.X,
                        y: bounds.Y,
                        width: bounds.Width,
                        height: bounds.Height,
                        scaleFactor: scaleFactor
                    );
                }
                return screenParamsArray;
            }
        }

        public static ScreenParams FindByPoint(Point point)
        {
            foreach (var screen in All)
                if (screen.Contains(point))
                    return screen;
            return null;
        }

        public Point CenterPoint => new Point(X + Width / 2, Y + Height / 2);

        public bool Contains(Point point) => point.X >= X && point.X < (X + Width) && point.Y >= Y && point.Y < (Y + Height);

        public string Describe(PreferredDescriptionType type) => $"Screen {Index}: {Width}x{Height}, At: {X}, {Y}";

        public override bool Equals(object obj)
        {
            if (obj is null) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((ScreenParams) obj);
        }

        public override int GetHashCode() => DeviceName != null ? DeviceName.GetHashCode() : 0;

        protected bool Equals(ScreenParams other) => string.Equals(DeviceName, other.DeviceName);

    }

}