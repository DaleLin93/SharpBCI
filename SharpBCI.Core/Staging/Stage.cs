using MarukoLib.Lang;
using Newtonsoft.Json;

namespace SharpBCI.Core.Staging
{

    [JsonObject(MemberSerialization.OptIn)]
    public class Stage : ContextObject
    {

        public static readonly IContextProperty TagProperty = new ContextProperty();

        [JsonProperty] public string Identifier;

        [JsonProperty] public string Cue;

        [JsonProperty] public string Subtitle;

        [JsonProperty] public ulong Duration;

        [JsonProperty] public int? Marker;

        public object Tag
        {
            get => TryGet(TagProperty, out var tag) ? tag : null;
            set => Set(TagProperty, value);
        }

        public T GetTagOfType<T>() where T : class => Tag as T;

        public T GetTagOfType<T>(T defaultVal) => Tag is T t ? t : defaultVal;

        public override string ToString() => 
            $"{nameof(Identifier)}: {Identifier}, {nameof(Cue)}: {Cue}, {nameof(Subtitle)}: {Subtitle}, {nameof(Duration)}: {Duration}, {nameof(Marker)}: {Marker}";

    }

}
