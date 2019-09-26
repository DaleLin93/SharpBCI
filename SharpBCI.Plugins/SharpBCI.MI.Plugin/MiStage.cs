using System;
using System.Collections.Generic;
using MarukoLib.Lang;
using SharpBCI.Core.Staging;

namespace SharpBCI.Paradigms.MI
{

    internal class MiStage : Stage
    {

        public enum VisualStimulusType
        {
            Text, Video, Image
        }

        public enum AuditoryStimulusType
        {
            Text, Audio
        }

        public sealed class Stimulus<T> where T : Enum
        {

            public readonly T Type;

            public readonly string Content;

            public Stimulus(T type, string content)
            {
                Type = type;
                Content = content;
            }

            public static Stimulus<T> Parse(string strVal)
            {
                if (strVal?.IsBlank() ?? true) return null;
                var colonIndex = strVal.IndexOf(':');
                if (colonIndex == -1) throw new ArgumentException("malformed stimulus: " + strVal);
                var typeStr = strVal.Substring(0, colonIndex);
                var array = Enum.GetValues(typeof(T));
                var found = false;
                T type = default;
                foreach (var e in array)
                    if (typeStr.Equals(e.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        type = (T)e;
                        found = true;
                        break;
                    }
                if (!found) throw new ArgumentException($"cannot find stimulus type({typeof(T).Name}) by name: {typeStr}");
                return new Stimulus<T>(type, strVal.Substring(colonIndex + 1));
            }

            public bool Equals(Stimulus<T> other) => EqualityComparer<T>.Default.Equals(Type, other.Type) && string.Equals(Content, other.Content);

            public override bool Equals(object obj)
            {
                if (null == obj) return false;
                if (ReferenceEquals(this, obj)) return true;
                return obj.GetType() == GetType() && Equals((Stimulus<T>) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (EqualityComparer<T>.Default.GetHashCode(Type) * 397) ^ (Content != null ? Content.GetHashCode() : 0);
                }
            }

            public override string ToString() => Content;

        }

        public string StimId;

        public Stimulus<VisualStimulusType> VisualStimulus;

        public Stimulus<AuditoryStimulusType> AuditoryStimulus;

        public bool ShowProgressBar;

        public bool IsPreload;

        public object DebugInfo;

        public override string ToString() => $"Id: {StimId ?? "None"}, Visual: {VisualStimulus?.ToString() ?? "None"}, Auditory: {AuditoryStimulus?.ToString() ?? "None"}";

    }
}