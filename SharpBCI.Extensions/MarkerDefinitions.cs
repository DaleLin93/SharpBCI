﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Reflection;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using Newtonsoft.Json;

namespace SharpBCI.Extensions
{

    public struct MarkerDefinition : IRegistrable
    {

        private const string CodeKey = nameof(Code);

        private const string NameKey = nameof(Name);

        private const string ColorKey = nameof(Color);

        [JsonConstructor]
        internal MarkerDefinition([JsonProperty(CodeKey)] int code, 
            [JsonProperty(NameKey)] string name, [JsonProperty(ColorKey)] uint color)
        {
            Code = code;
            Name = name;
            Color = color;
        }

        [JsonProperty(CodeKey)]
        public int Code { get; }

        [JsonProperty(NameKey)]
        public string Name { get; }

        [JsonProperty(ColorKey)]
        public uint Color { get; }

        public override string ToString() => Name;

        string IRegistrable.Identifier => Name;

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

        public static readonly IRegistry<MarkerDefinition> MarkerRegistry;

        public static readonly IDictionary<int, MarkerDefinition> GlobalMarkers;

        public const int GlobalMarkerBase = 0;

        public const int SessionMarkerBase = 10;

        public const int ParadigmMarkerBase = 20;

        public const int BlockMarkerBase = 30;

        public const int TrialMarkerBase = 40;

        public const int CustomMarkerBase = 100;

        #region Etctera Part

        [MarkerDefinition(GlobalGroupName)] public const int HeartbeatMarker = GlobalMarkerBase + 0;

        [MarkerDefinition(GlobalGroupName + ":baseline")] public const int BaselineStartMarker = GlobalMarkerBase + 1;

        [MarkerDefinition(GlobalGroupName + ":baseline")] public const int BaselineEndMarker = GlobalMarkerBase + 2;
        
        #endregion

        #region Session Part

        [MarkerDefinition(GlobalGroupName + ":session", 0xFFCC0000)] public const int SessionStartMarker = SessionMarkerBase + 1;

        [MarkerDefinition(GlobalGroupName + ":session", 0xFFCC0000)] public const int SessionEndMarker = SessionMarkerBase + 2;

        [MarkerDefinition(GlobalGroupName + ":session", 0xFFCC0000)] public const int UserExitMarker = SessionMarkerBase + 9;

        #endregion

        #region Paradigm Part

        [MarkerDefinition(GlobalGroupName + ":paradigm")] public const int ParadigmStartMarker = ParadigmMarkerBase + 1;

        [MarkerDefinition(GlobalGroupName + ":paradigm")] public const int ParadigmEndMarker = ParadigmMarkerBase + 2;

        #endregion

        #region Block Part

        [MarkerDefinition(GlobalGroupName + ":block")] public const int BlockStartMarker = BlockMarkerBase + 1;

        [MarkerDefinition(GlobalGroupName + ":block")] public const int BlockEndMarker = BlockMarkerBase + 2;

        #endregion

        #region Trial Part

        [MarkerDefinition(GlobalGroupName + ":trial", 0xFF008800)] public const int TrialStartMarker = TrialMarkerBase + 1;

        [MarkerDefinition(GlobalGroupName + ":trial", 0xFF880000)] public const int TrialEndMarker = TrialMarkerBase + 2;

        #endregion

        static MarkerDefinitions()
        {
            GlobalMarkers = new ReadOnlyDictionary<int, MarkerDefinition>(GetDefinedMarkers(typeof(MarkerDefinitions)));
            var registry = new Registry(typeof(MarkerDefinition));
            foreach (var globalMarker in GlobalMarkers.Values)
                registry.Register(globalMarker);
            MarkerRegistry = new Registry<MarkerDefinition>(new ComplexRegistry(registry));
        }

        public static IDictionary<int, MarkerDefinition> GetDefinedMarkers(Type type)
        {
            var dict = new Dictionary<int, MarkerDefinition>();
            foreach (var field in type.GetRuntimeFields())
            {
                var attr = field.GetCustomAttribute<MarkerDefinitionAttribute>();
                if (attr == null) continue;
                var marker = (int)field.GetValue(null);
                var name = $"{attr.GroupName}:{attr.Name ?? field.Name.TrimEnd("Marker")}";
                if (marker < CustomMarkerBase && !attr.GroupName.StartsWith(GlobalGroupName))
                    throw new ProgrammingException($"Marker of {type.FullName}.{name} is reserved(less than {CustomMarkerBase})");
                if (dict.ContainsKey(marker)) throw new ProgrammingException($"Duplicated marker in type: {type.FullName}, {dict[marker]} and {name}");
                dict[marker] = new MarkerDefinition(marker, name, attr.Color);
            }
            return dict;
        }

    }

}
