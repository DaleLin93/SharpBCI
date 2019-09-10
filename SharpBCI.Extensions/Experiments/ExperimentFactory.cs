using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.Experiments
{

    public struct ValidationResult
    {

        public static readonly ValidationResult Ok = new ValidationResult(false, null);

        public static readonly ValidationResult FailedNoMessage = new ValidationResult(true, null);

        public readonly bool IsFailed;

        [CanBeNull]
        public readonly string Message;

        private ValidationResult(bool failed, [CanBeNull]string message)
        {
            IsFailed = failed;
            Message = message;
        }

        public static ValidationResult Failed([CanBeNull] string message = null) => new ValidationResult(true, message);

    }

    public interface IExperimentFactory
    {

        Type ExperimentType { get; }

        IReadOnlyCollection<ParameterGroup> ParameterGroups { get; }

        IReadOnlyCollection<ISummary> Summaries { get; }

        /// <summary>
        /// Post validation after all parameters are validated separately and before experiment creation.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        ValidationResult IsValid(IReadonlyContext context, IParameterDescriptor parameter);

        /// <summary>
        /// Create experiment instance.
        /// </summary>
        /// <param name="context">Experiment parameters</param>
        /// <exception cref="ExperimentInitiationException"></exception>
        /// <returns></returns>
        IExperiment Create(IReadonlyContext context);

    }

    public abstract class ExperimentFactory<T> : IExperimentFactory, IParameterPresentAdapter, ISummaryPresentAdapter where T : IExperiment
    {

        public static IReadOnlyCollection<ParameterGroup> ScanGroups(Type type, bool findUngroupedParameters = true, bool recursively = true)
        {
            var groups = type.ReadFields<ParameterGroup>(null, recursively);
            var rootGroups = new LinkedList<ParameterGroup>(groups);
            foreach (var group in groups)
                foreach (var parameterGroup in group.GetAllGroups(false))
                    rootGroups.Remove(parameterGroup);
            if (!findUngroupedParameters) return rootGroups;
            var parameters = type.ReadFields<IParameterDescriptor>(null, recursively);
            var unusedParameters = new HashSet<IParameterDescriptor>();
            foreach (var parameter in parameters)
                unusedParameters.Add(parameter);
            foreach (var parameter in rootGroups.GetAllParameters())
                unusedParameters.Remove(parameter);
            if (unusedParameters.Any())
                return new List<ParameterGroup>(rootGroups) { new ParameterGroup(null, null, unusedParameters) };
            return rootGroups;
        }

        public static IReadOnlyCollection<ISummary> ScanSummaries(Type type, bool recursively = true) => type.ReadFields<ISummary>(null, recursively);

        public Type ExperimentType => typeof(T);

        public virtual IReadOnlyCollection<ParameterGroup> ParameterGroups => EmptyArray<ParameterGroup>.Instance;

        public virtual IReadOnlyCollection<ISummary> Summaries => EmptyArray<ISummary>.Instance;

        public virtual bool IsVisible(IReadonlyContext context, IDescriptor descriptor) => true;

        public virtual bool IsVisible(IReadonlyContext context, ISummary summary) => true;

        public virtual bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => true;

        public virtual ValidationResult IsValid(IReadonlyContext context, IParameterDescriptor parameter) => ValidationResult.Ok;

        public abstract T Create(IReadonlyContext context);

        IExperiment IExperimentFactory.Create(IReadonlyContext context) => Create(context);

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class ExperimentAttribute : Attribute
    {

        public ExperimentAttribute([NotNull] string name, [CanBeNull] string version = null, [CanBeNull] string versionName = null)
        {
            Name = name.Trim2Null() ?? throw new ArgumentException(nameof(name));
            Version = version == null ? null : Version.Parse(version);
            VersionName = versionName?.Trim2Null();
        }

        [NotNull] public string Name { get; }

        [CanBeNull] public Version Version { get; }

        [CanBeNull] public string VersionName { get; }

        public string FullVersionName
        {
            get
            {
                var versionStr = Version == null ? "un-versioned" : $"v{Version}";
                return VersionName == null ? versionStr : $"{versionStr}-{VersionName}";
            }
        }

        public string Description { get; set; }

    }

    public static class ExperimentExt
    {

        public static ExperimentAttribute GetExperimentAttribute(this Type type)
        {
            if (!typeof(IExperiment).IsAssignableFrom(type)) throw new ArgumentException($"Given type '{type.FullName}' must implements interface IExperiment");
            return type.GetCustomAttribute<ExperimentAttribute>() ?? throw new ProgrammingException($"ExperimentAttribute not declared, type: '{type.FullName}'");
        }

    }

}
