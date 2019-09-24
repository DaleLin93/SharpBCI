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

    /// <summary>
    /// Interface of experiment factories.
    /// </summary>
    public interface IExperimentFactory
    {

        /// <summary>
        /// Get definitions of parameters used to create the experiment.
        /// </summary>
        [NotNull] IReadOnlyCollection<IGroupDescriptor> GetParameterGroups(Type experimentClass);

        /// <summary>
        /// Get summaries used to peek the information of experiment while creating.
        /// </summary>
        [NotNull] IReadOnlyCollection<ISummary> GetSummaries(Type experimentClass);

        /// <summary>
        /// Post validation after all parameters are validated separately and before experiment creation.
        /// </summary>
        /// <param name="experimentClass"></param>
        /// <param name="context"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        ValidationResult CheckValid(Type experimentClass, IReadonlyContext context, IParameterDescriptor parameter);

        /// <summary>
        /// Create experiment instance.
        /// </summary>
        /// <param name="experimentClass"></param>
        /// <param name="context">Experiment parameters</param>
        /// <returns></returns>
        [NotNull] IExperiment Create(Type experimentClass, IReadonlyContext context);

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class ExperimentAttribute : Attribute
    {

        public ExperimentAttribute([NotNull] string name, [NotNull] Type factoryType, [CanBeNull] string version = null, [CanBeNull] string versionName = null)
        {
            Name = name.Trim2Null() ?? throw new ArgumentException(nameof(name));
            FactoryType = factoryType ?? throw new ArgumentException(nameof(factoryType));
            Version = version == null ? null : Version.Parse(version);
            VersionName = versionName?.Trim2Null();
        }

        [NotNull] public string Name { get; }

        [NotNull] public Type FactoryType { get; }

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

    public abstract class ExperimentFactory : IExperimentFactory 
    {

        public static IReadOnlyCollection<IGroupDescriptor> ScanGroups(Type type, bool findUngroupedParameters = true, bool recursively = true)
        {
            var groups = type.ReadFields<IGroupDescriptor>(null, recursively);
            var rootGroups = new LinkedList<IGroupDescriptor>(groups);
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
                return new List<IGroupDescriptor>(rootGroups) { new ParameterGroup(null, null, unusedParameters) };
            return rootGroups;
        }

        public static IReadOnlyCollection<ISummary> ScanSummaries(Type type, bool recursively = true) => type.ReadFields<ISummary>(null, recursively);

        public virtual IReadOnlyCollection<IGroupDescriptor> GetParameterGroups(Type experimentClass) => EmptyArray<IGroupDescriptor>.Instance;

        public virtual IReadOnlyCollection<ISummary> GetSummaries(Type experimentClass) => EmptyArray<ISummary>.Instance;

        public virtual ValidationResult CheckValid(Type experimentClass, IReadonlyContext context, IParameterDescriptor parameter) => ValidationResult.Ok;

        public abstract IExperiment Create(Type experimentClass, IReadonlyContext context);

    }

    public abstract class ExperimentFactory<T> : ExperimentFactory, IParameterPresentAdapter, ISummaryPresentAdapter where T : IExperiment
    {

        public Type ExperimentType => typeof(T);

        public virtual IReadOnlyCollection<IGroupDescriptor> ParameterGroups => EmptyArray<IGroupDescriptor>.Instance;

        public virtual IReadOnlyCollection<ISummary> Summaries => EmptyArray<ISummary>.Instance;

        public virtual bool CanReset(IParameterDescriptor parameter) => true;

        public virtual bool CanCollapse(IGroupDescriptor group, int depth) => true;

        public virtual bool IsVisible(IReadonlyContext context, IDescriptor descriptor) => true;

        public virtual bool IsVisible(IReadonlyContext context, ISummary summary) => true;

        public virtual bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => true;

        public virtual ValidationResult CheckValid(IReadonlyContext context, IParameterDescriptor parameter) => ValidationResult.Ok;

        public sealed override IReadOnlyCollection<IGroupDescriptor> GetParameterGroups(Type experimentClass) => ParameterGroups;

        public sealed override IReadOnlyCollection<ISummary> GetSummaries(Type experimentClass) => Summaries;

        public sealed override ValidationResult CheckValid(Type experimentClass, IReadonlyContext context, IParameterDescriptor parameter) => CheckValid(context, parameter);

        public sealed override IExperiment Create(Type experimentClass, IReadonlyContext context) => Create(context);

        public abstract T Create(IReadonlyContext context);

    }

}
