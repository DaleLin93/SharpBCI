using MarukoLib.Lang;
using Newtonsoft.Json;
using SharpBCI.Extensions.Presenters;

namespace SharpBCI.Extensions.Data
{

    [Presenter(typeof(PathPresenter))]
    public sealed class Path
    {

        private const string PathKey = "Path";

        public static readonly ITypeConverter<Path, string> TypeConverter = TypeConverterExt.OfNull2Null<Path, string>(path => path.Value, path => new Path(path));

        [JsonConstructor]
        public Path([JsonProperty(PathKey)] string path) => Value = path;

        [JsonProperty(PathKey)]
        public string Value { get; }

        public bool Exists => System.IO.File.Exists(Value) || System.IO.Directory.Exists(Value);

    }

}
