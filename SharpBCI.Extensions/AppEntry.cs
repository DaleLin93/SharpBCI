using System;
using System.Reflection;
using JetBrains.Annotations;

namespace SharpBCI.Extensions
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class AppEntryAttribute : Attribute
    {

        public AppEntryAttribute([NotNull] string name, bool autoStart = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            AutoStart = autoStart;
        }

        public string Name { get; }

        public bool AutoStart { get; }

    }

    public interface IAppEntry
    {

        void Run();

    }

    public static class AppEntryExt
    {

        [NotNull]
        public static AppEntryAttribute GetAppEntryAttribute(this Type type)
        {
            if (!typeof(IAppEntry).IsAssignableFrom(type)) throw new ArgumentException($"Given type '{type.FullName}' must implements interface IAppEntry");
            return type.GetCustomAttribute<AppEntryAttribute>() ?? throw new ArgumentException($"AppEntryAttribute not defined for '{type.FullName}'");
        }

    }


}
