using System;
using System.Reflection;
using JetBrains.Annotations;

namespace SharpBCI.Extensions
{

    /// <summary>
    /// An attribute to supply extra information for AppEntry. 
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class AppEntryAttribute : Attribute
    {

        public AppEntryAttribute([NotNull] string name, bool autoStart = false)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            AutoStart = autoStart;
        }

        /// <summary>
        /// Name of app entry.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Run automatically while the system is started.
        /// </summary>
        public bool AutoStart { get; }

    }

    /// <summary>
    /// An interface that define an entry for the application.
    /// Attribute <see cref="AppEntryAttribute"/> must be declared for every implementation.
    /// </summary>
    public interface IAppEntry
    {

        /// <summary>
        /// Entry method, to run the application.
        /// </summary>
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
