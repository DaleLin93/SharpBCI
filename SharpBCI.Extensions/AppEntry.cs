using System;
using System.Reflection;
using JetBrains.Annotations;

namespace SharpBCI.Extensions
{

    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class AppEntryAttribute : Attribute
    {

        public AppEntryAttribute(bool autoStart) => AutoStart = autoStart;

        public bool AutoStart { get; }

    }

    public interface IAppEntry
    {

        string Name { get; }

        void Run();

    }

    public static class AppEntryExt
    {

        [CanBeNull]
        public static AppEntryAttribute GetAppEntryAttribute(this Type type)
        {
            if (!typeof(IAppEntry).IsAssignableFrom(type))
                throw new ArgumentException($"Given type '{type.FullName}' must implements interface IAppEntry");
            return type.GetCustomAttribute<AppEntryAttribute>();
        }

    }


}
