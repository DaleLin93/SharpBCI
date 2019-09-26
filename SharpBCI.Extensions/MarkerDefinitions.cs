using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;

namespace SharpBCI.Extensions
{

    public struct MarkerDefinition
    {

        internal MarkerDefinition(string name, uint color)
        {
            Name = name;
            Color = color;
        }

        public string Name { get; }

        public uint Color { get; }

        public override string ToString() => Name;

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, Inherited = false)]
    public class MarkerDefinitionAttribute : Attribute
    {

        public const uint DefaultColor = 0xFF000000;

        public MarkerDefinitionAttribute(string groupName) : this(groupName, null, DefaultColor) { }

        public MarkerDefinitionAttribute(string groupName, string name) : this(groupName, name, DefaultColor) { }

        public MarkerDefinitionAttribute(string groupName, uint color) : this(groupName, null, color) { }

        public MarkerDefinitionAttribute(string groupName, string name, uint color)
        {
            GroupName = groupName.Trim2Null() ?? throw new ArgumentException("'groupName' cannot be blank or null");
            Name = name?.Trim2Null();
            Color = color;
        }

        public string GroupName { get; }

        public string Name { get; set; }

        public uint Color { get; set; }

    }

    public static class MarkerDefinitions
    {

        private const string GlobalGroupName = "global";

        public static IDictionary<int, MarkerDefinition> GlobalMarkers = new ReadOnlyDictionary<int, MarkerDefinition>(GetDefinedMarkers(typeof(MarkerDefinitions)));

        public const int GlobalMarkerBase = 0;

        public const int CustomMarkerBase = 100;

        [MarkerDefinition(GlobalGroupName + ":paradigm")] public const int ParadigmStartMarker = GlobalMarkerBase + 1;

        [MarkerDefinition(GlobalGroupName + ":paradigm")] public const int ParadigmEndMarker = GlobalMarkerBase + 2;

        [MarkerDefinition(GlobalGroupName + ":trial", 0xFF008800)] public const int TrialStartMarker = GlobalMarkerBase + 11;

        [MarkerDefinition(GlobalGroupName + ":trial", 0xFF880000)] public const int TrialEndMarker = GlobalMarkerBase + 12;

        [MarkerDefinition(GlobalGroupName + ":baseline")] public const int BaselineStartMarker = GlobalMarkerBase + 21;

        [MarkerDefinition(GlobalGroupName + ":baseline")] public const int BaselineEndMarker = GlobalMarkerBase + 22;

        [MarkerDefinition(GlobalGroupName + ":session", 0xFFCC0000)] public const int UserExitMarker = GlobalMarkerBase + 31;

        public static IDictionary<int, MarkerDefinition> GetDefinedMarkers(Type type)
        {
            var dict = new Dictionary<int, MarkerDefinition>();
            foreach (var field in type.GetRuntimeFields())
            {
                var attribute = field.GetCustomAttribute<MarkerDefinitionAttribute>();
                if (attribute == null) continue;
                var marker = (int)field.GetValue(null);
                var markerName = $"{attribute.GroupName}:{attribute.Name ?? field.Name.TrimEnd("Marker")}";
                if (marker < CustomMarkerBase && !attribute.GroupName.StartsWith($"{GlobalGroupName}:"))
                    throw new ProgrammingException($"Marker of {type.FullName}.{markerName} is reserved(less than {CustomMarkerBase})");
                if (dict.ContainsKey(marker)) throw new ProgrammingException($"Duplicated marker in type: {type.FullName}, {dict[marker]} and {markerName}");
                dict[marker] = new MarkerDefinition(markerName, attribute.Color);
            }
            return dict;
        }

    }

}
