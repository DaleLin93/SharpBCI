using Newtonsoft.Json;

namespace SharpBCI.Experiments.WebBrowser
{

#pragma warning disable 169
    public abstract class Message
    {

        [JsonProperty("type")]
        public string Type;

    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class IncomingMessage : Message
    {

        [JsonProperty("focused")]
        public bool? Focused;

    }

    [JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
    public class OutgoingMessage : Message
    {

        public struct VisualScheme
        {

            [JsonProperty("frequency")]
            public float Frequency;

            [JsonProperty("borderThickness")]
            public string BorderThickness;

            [JsonProperty("borderStyle")]
            public string BorderStyle;

            [JsonProperty("color")]
            public string Color;

        }

        public struct Point
        {

            [JsonProperty("x")]
            public double X;

            [JsonProperty("y")]
            public double Y;

        }

        [JsonProperty("debug")]
        public bool? Debug;

        [JsonProperty("visualSchemes")]
        public VisualScheme[] VisualSchemes;

        [JsonProperty("stimulationSize")]
        public Point? StimulationSize;

        [JsonProperty("maxActiveDistance")]
        public uint MaxActiveDistance;

        [JsonProperty("confirmationDelay")]
        public uint? ConfirmationDelay;

        [JsonProperty("homePage")]
        public string HomePage;

        [JsonProperty("gazePoint")]
        public Point? GazePoint;

        [JsonProperty("frequencyIndex")]
        public int? FrequencyIndex;

    }
#pragma warning restore 169

}
