using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace SharpBCI.Paradigms.WebBrowser
{

#pragma warning disable 169
    public struct Point
    {

        public double X, Y;

        public System.Windows.Point ToSwPoint() => new System.Windows.Point(X, Y);

    }

    public struct Size
    {

        public double Width, Height;

        public System.Windows.Size ToSwSize() => new System.Windows.Size(Width, Height);

    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum Scene
    {
        Page, Keyboard
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum Mode : byte
    {
        Normal, Reading
    }

    public abstract class Message
    {

        public string Type;

    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class IncomingMessage : Message
    {

        public Scene? Scene;

        public Mode? Mode;

        public bool? Focused;

        public Point? WindowPosition;

        public Point? ScrollPosition;

        public Size? WindowOuterSize;

        public Size? WindowInnerSize;

        public Size? DocumentSize;

    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class OutgoingMessage : Message
    {

        public struct VisualScheme
        {

            public float Frequency;

            public string BorderThickness;

            public string BorderStyle;

            public string Color;

        }

        public bool? Debug;

        public VisualScheme[] VisualSchemes;

        public Point? StimulationSize;

        public uint? MaxActiveDistance;

        public uint? ConfirmationDelay;

        public bool? EdgeScrolling;

        public string HomePage;

        public Point? GazePoint;

        public Point? ScrollDistance;

        public int? FrequencyIndex;

        public Mode? Mode;

    }
#pragma warning restore 169

}
