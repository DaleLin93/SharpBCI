using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using Newtonsoft.Json;

namespace SharpBCI.Extensions
{

    [JsonObject(MemberSerialization.OptIn)]
    public struct MarkerDefinition : IRegistrable
    {

        public const uint DefaultColor = 0xFF000000;

        private const string CodeKey = nameof(Code);

        private const string OwnerKey = nameof(Owner);

        private const string NamespaceKey = nameof(Namespace);

        private const string NameKey = nameof(Name);

        private const string ColorKey = nameof(Color);

        [JsonConstructor]
        internal MarkerDefinition([JsonProperty(CodeKey)] int code,
            [JsonProperty(OwnerKey), NotNull] string owner,
            [JsonProperty(NamespaceKey), NotNull] string @namespace, 
            [JsonProperty(NameKey), NotNull] string name, 
            [JsonProperty(ColorKey)] uint color)
        {
            Code = code;
            Owner = owner;
            Namespace = @namespace;
            Name = name;
            Color = color;
        }

        [JsonProperty(CodeKey)]
        public int Code { get; }

        /// <summary>
        /// The owner (owned class) of the marker.
        /// </summary>
        [JsonProperty(OwnerKey), NotNull]
        public string Owner { get; }

        [JsonProperty(NamespaceKey), NotNull]
        public string Namespace { get; }

        [JsonProperty(NameKey), NotNull]
        public string Name { get; }

        [JsonProperty(ColorKey)]
        public uint Color { get; }

        public string FullName => $"{Namespace}:{Name}";

        public override string ToString() => FullName;

        string IRegistrable.Identifier => FullName;

    }

    [JsonObject(MemberSerialization.OptIn)]
    public struct MarkerNamespaceDefinition : IRegistrable
    {

        public const uint DefaultColor = 0xFF2F4F4F;

        private const string PathKey = nameof(Path);

        private const string OwnerKey = nameof(Owner);

        private const string ColorKey = nameof(Color);

        [JsonConstructor]
        internal MarkerNamespaceDefinition(
            [JsonProperty(PathKey), NotNull] string path,
            [JsonProperty(OwnerKey), NotNull] string owner,
            [JsonProperty(ColorKey)] uint color)
        {
            Path = path;
            Owner = owner;
            Color = color;
        }

        [JsonProperty(PathKey), NotNull]
        public string Path { get; }

        [JsonProperty(OwnerKey), NotNull]
        public string Owner { get; }

        [JsonProperty(ColorKey)]
        public uint Color { get; }

        public override string ToString() => Path;

        string IRegistrable.Identifier => Path;

    }

    /// <summary>
    /// An attribute to supply extra information for MarkerNamespace. 
    /// The available target for attribute is the field with type <see cref="string"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public class MarkerNamespaceAttribute : Attribute
    {

        public MarkerNamespaceAttribute(uint color = MarkerNamespaceDefinition.DefaultColor) => Color = color;

        /// <summary>
        /// The color of the namespace.
        /// </summary>
        public uint Color { get; }

    }

    /// <summary>
    /// An attribute to define markers and provide extra information.
    /// The available target for attribute is the field with type <see cref="int"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, Inherited = false)]
    public class MarkerAttribute : Attribute
    {

        public MarkerAttribute([NotNull] string @namespace) : this(@namespace, null) { }

        public MarkerAttribute([NotNull] string @namespace, uint color) : this(@namespace, null, color) { }

        public MarkerAttribute([NotNull] string @namespace, [CanBeNull] string name, uint color = MarkerDefinition.DefaultColor)
        {
            Namespace = @namespace.Trim2Null() ?? throw new ArgumentException($"'{nameof(@namespace)}' cannot be blank or null");
            Name = name?.Trim2Null();
            Color = color;
        }

        /// <summary>
        /// The namespace of marker.
        /// </summary>
        [NotNull] public string Namespace { get; }

        /// <summary>
        /// The name of marker. The field name will be used as marker name if the name is unspecified.
        /// </summary>
        [CanBeNull] public string Name { get; set; }

        /// <summary>
        /// The color of the marker.
        /// </summary>
        public uint Color { get; }

    }

    public static class MarkerDefinitions
    {

        private const string GroupSeparator = ":";

        public static readonly IRegistry<MarkerNamespaceDefinition> NamespaceRegistry;

        public static readonly IRegistry<MarkerDefinition> MarkerRegistry;

        public static readonly IReadOnlyDictionary<int, MarkerDefinition> GlobalMarkers;

        #region Marker Group Definitions

        public const int GlobalMarkerBase = 0;

        public const int ParadigmMarkerBase = 10;

        public const int SessionMarkerBase = 20;

        public const int BlockMarkerBase = 30;

        public const int TrialMarkerBase = 40;

        public const int CustomMarkerBase = 100;

        [MarkerNamespace(0xFF000000)] public const string GlobalMarkerGroup = "global";

        [MarkerNamespace(0xFF888888)] public const string BaselineMarkerGroup = GlobalMarkerGroup + GroupSeparator + "baseline";

        [MarkerNamespace(0xFF884444)] public const string ParadigmMarkerGroup = GlobalMarkerGroup + GroupSeparator + "paradigm";

        [MarkerNamespace(0xFF448844)] public const string SessionMarkerGroup = GlobalMarkerGroup + GroupSeparator + "session";

        [MarkerNamespace(0xFF444488)] public const string BlockMarkerGroup = GlobalMarkerGroup + GroupSeparator + "block";

        [MarkerNamespace(0xFF448888)] public const string TrialMarkerGroup = GlobalMarkerGroup + GroupSeparator + "trial";

        #endregion

        #region Etctera Marker Definitions

        [Marker(GlobalMarkerGroup)] public const int HeartbeatMarker = GlobalMarkerBase + 0;

        [Marker(BaselineMarkerGroup)] public const int BaselineStartMarker = GlobalMarkerBase + 1;

        [Marker(BaselineMarkerGroup)] public const int BaselineEndMarker = GlobalMarkerBase + 2;

        #endregion

        #region Paradigm Marker Definitions

        [Marker(ParadigmMarkerGroup)] public const int ParadigmStartMarker = ParadigmMarkerBase + 1;

        [Marker(ParadigmMarkerGroup)] public const int ParadigmEndMarker = ParadigmMarkerBase + 2;

        #endregion

        #region Session Marker Definitions

        [Marker(SessionMarkerGroup, 0xFF119922)] public const int SessionStartMarker = SessionMarkerBase + 1;

        [Marker(SessionMarkerGroup, 0xFFBB0000)] public const int SessionEndMarker = SessionMarkerBase + 2;

        [Marker(SessionMarkerGroup, 0xFFFF0000)] public const int UserExitMarker = SessionMarkerBase + 9;

        #endregion

        #region Block Marker Definitions

        [Marker(BlockMarkerGroup, 0xFF119922)] public const int BlockStartMarker = BlockMarkerBase + 1;

        [Marker(BlockMarkerGroup, 0xFFBB0000)] public const int BlockEndMarker = BlockMarkerBase + 2;

        #endregion

        #region Trial Marker Definitions

        [Marker(TrialMarkerGroup, 0xFF119922)] public const int TrialStartMarker = TrialMarkerBase + 1;

        [Marker(TrialMarkerGroup, 0xFFBB0000)] public const int TrialEndMarker = TrialMarkerBase + 2;

        #endregion

        static MarkerDefinitions()
        {
            GetDefinedMarkers(typeof(MarkerDefinitions), out var namespaces, out var markers);
            GlobalMarkers = new ReadOnlyDictionary<int, MarkerDefinition>(markers);
            var namespaceRegistry = new Registry(typeof(MarkerNamespaceDefinition));
            namespaceRegistry.RegisterAll(namespaces.Values);
            NamespaceRegistry = new ComplexRegistry(namespaceRegistry).OfType<MarkerNamespaceDefinition>();
            var markerRegistry = new Registry(typeof(MarkerDefinition));
            markerRegistry.RegisterAll(markers.Values);
            MarkerRegistry = new ComplexRegistry(markerRegistry).OfType<MarkerDefinition>();
        }

        public static void GetDefinedMarkers(Type type, out IDictionary<string, MarkerNamespaceDefinition> namespaces, out IDictionary<int, MarkerDefinition> markers)
        {
            namespaces = new Dictionary<string, MarkerNamespaceDefinition>();
            markers = new Dictionary<int, MarkerDefinition>();
            foreach (var field in type.GetRuntimeFields())
            {
                if (TryGetMarkerNamespaceDefinition(field, out var mnd))
                {
                    if (namespaces.ContainsKey(mnd.Path))
                        throw new ProgrammingException($"Duplicated marker in type: {mnd.Owner}, {namespaces[mnd.Path]}");
                    namespaces[mnd.Path] = mnd;
                } 
                if (TryGetMarkerDefinition(field, out var md))
                {
                    if (markers.ContainsKey(md.Code))
                        throw new ProgrammingException($"Duplicated marker in type: {md.Owner}, {markers[md.Code]} and {md.Name}");
                    markers[md.Code] = md;
                }
            }
        }

        public static bool TryGetMarkerNamespaceDefinition(FieldInfo field, out MarkerNamespaceDefinition definition)
        {
            definition = default;
            var attr = field.GetCustomAttribute<MarkerNamespaceAttribute>();
            if (attr == null) return false;
            if (field.FieldType != typeof(string)) throw new ProgrammingException($"Field '{field}' must be defined as type 'string'");
            var path = (string)field.GetValue(null) ?? throw new ProgrammingException($"Field '{field}' must be a non-null value"); ;
            var typeFullName = field.DeclaringType?.FullName ?? "<null>";
            definition = new MarkerNamespaceDefinition(path, typeFullName, attr.Color);
            return true;
        }

        public static bool TryGetMarkerDefinition(FieldInfo field, out MarkerDefinition definition)
        {
            definition = default;
            var attr = field.GetCustomAttribute<MarkerAttribute>();
            if (attr == null) return false;
            if (field.FieldType != typeof(int)) throw new ProgrammingException($"Field '{field}' must be defined as type 'int'");
            var marker = (int)field.GetValue(null);
            var typeFullName = field.DeclaringType?.FullName ?? "<null>";
            var @namespace = attr.Namespace.TrimEnd(':');
            var name = attr.Name ?? field.Name.TrimEnd("Marker");
            if (marker < CustomMarkerBase && !@namespace.StartsWith($"{GlobalMarkerGroup}:") && !@namespace.Equals(GlobalMarkerGroup))
                throw new ProgrammingException($"Marker of {typeFullName}.{@namespace}:{name} is reserved(less than {CustomMarkerBase})");
            definition = new MarkerDefinition(marker, typeFullName, @namespace, name, attr.Color);
            return true;
        }

    }

}
