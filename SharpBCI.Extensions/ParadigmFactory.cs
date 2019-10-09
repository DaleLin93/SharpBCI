using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions
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
    /// Interface of paradigm factories.
    /// </summary>
    public interface IParadigmFactory
    {

        /// <summary>
        /// Get definitions of parameters used to create the paradigm.
        /// </summary>
        [NotNull] IReadOnlyCollection<IGroupDescriptor> GetParameterGroups(Type paradigmClz);

        /// <summary>
        /// Get summaries used to peek the information of paradigm while creating.
        /// </summary>
        [NotNull] IReadOnlyCollection<ISummary> GetSummaries(Type paradigmClz);

        /// <summary>
        /// Post validation after all parameters are validated separately and before paradigm creation.
        /// </summary>
        /// <param name="paradigmClz"></param>
        /// <param name="context"></param>
        /// <param name="parameter"></param>
        /// <returns></returns>
        ValidationResult CheckValid(Type paradigmClz, IReadonlyContext context, IParameterDescriptor parameter);

        /// <summary>
        /// Create paradigm instance.
        /// </summary>
        /// <param name="paradigmClz"></param>
        /// <param name="context">Paradigm parameters</param>
        /// <returns></returns>
        [NotNull] IParadigm Create(Type paradigmClz, IReadonlyContext context);

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class ParadigmAttribute : Attribute
    {

        public ParadigmAttribute([NotNull] string name, [NotNull] Type factoryType, [CanBeNull] string version = null, [CanBeNull] string versionName = null)
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

    public abstract class ParadigmFactory : IParadigmFactory 
    {

        public static IReadOnlyCollection<IGroupDescriptor> ScanGroups(Type type, bool findParametersNotInGroups = true, bool recursively = true)
        {
            var groups = type.ReadFields<IGroupDescriptor>(null, recursively);
            var rootGroups = new LinkedList<IGroupDescriptor>(groups);
            foreach (var group in groups)
                foreach (var parameterGroup in group.GetAllGroups(false))
                    rootGroups.Remove(parameterGroup);
            if (!findParametersNotInGroups) return rootGroups.AsReadonly();
            var parameters = type.ReadFields<IParameterDescriptor>(null, recursively);
            var unusedParameters = new HashSet<IParameterDescriptor>();
            foreach (var parameter in parameters)
                unusedParameters.Add(parameter);
            foreach (var parameter in rootGroups.GetAllParameters())
                unusedParameters.Remove(parameter);
            var extraGroup = new ParameterGroup(null, null, unusedParameters.AsReadonly());
            return unusedParameters.Any()
                ? new List<IGroupDescriptor>(rootGroups) {extraGroup}
                : rootGroups.AsReadonly();
        }

        public static IReadOnlyCollection<ISummary> ScanSummaries(Type type, bool recursively = true) => type.ReadFields<ISummary>(null, recursively).AsReadonly();

        public virtual IReadOnlyCollection<IGroupDescriptor> GetParameterGroups(Type paradigmClz) => EmptyArray<IGroupDescriptor>.Instance;

        public virtual IReadOnlyCollection<ISummary> GetSummaries(Type paradigmClz) => EmptyArray<ISummary>.Instance;

        public virtual ValidationResult CheckValid(Type paradigmClz, IReadonlyContext context, IParameterDescriptor parameter) => ValidationResult.Ok;

        public abstract IParadigm Create(Type paradigmClz, IReadonlyContext context);

    }

    public abstract class ParadigmFactory<T> : ParadigmFactory, IParameterPresentAdapter, ISummaryPresentAdapter where T : IParadigm
    {

        public Type ParadigmType => typeof(T);

        public virtual IReadOnlyCollection<IGroupDescriptor> ParameterGroups => EmptyArray<IGroupDescriptor>.Instance;

        public virtual IReadOnlyCollection<ISummary> Summaries => EmptyArray<ISummary>.Instance;

        public virtual bool CanReset(IParameterDescriptor parameter) => true;

        public virtual bool CanCollapse(IGroupDescriptor group, int depth) => true;

        public virtual bool IsVisible(IReadonlyContext context, IDescriptor descriptor) => true;

        public virtual bool IsVisible(IReadonlyContext context, ISummary summary) => true;

        public virtual bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => true;

        public virtual ValidationResult CheckValid(IReadonlyContext context, IParameterDescriptor parameter) => ValidationResult.Ok;

        public sealed override IReadOnlyCollection<IGroupDescriptor> GetParameterGroups(Type paradigmClz) => ParameterGroups;

        public sealed override IReadOnlyCollection<ISummary> GetSummaries(Type paradigmClz) => Summaries;

        public sealed override ValidationResult CheckValid(Type paradigmClz, IReadonlyContext context, IParameterDescriptor parameter) => CheckValid(context, parameter);

        public sealed override IParadigm Create(Type paradigmClz, IReadonlyContext context) => Create(context);

        public abstract T Create(IReadonlyContext context);

    }

    public class AutoParadigmFactory : ParadigmFactory
    {

        private interface IInitializer
        {

            IDescriptor[] Descriptors { get; }

            object Get(IReadonlyContext context);

        }

        private class NullInitializer : IInitializer
        {

            internal static readonly NullInitializer Instance = new NullInitializer();

            private NullInitializer() { }

            public IDescriptor[] Descriptors => EmptyArray<IDescriptor>.Instance;

            public object Get(IReadonlyContext context) => null;

        }

        private class ValueInitializer : IInitializer
        {

            private readonly AutoParameter _parameter;

            private readonly IAutoParamAdapter _adapter;

            internal ValueInitializer(AutoParameter parameter, IAutoParamAdapter adapter)
            {
                _parameter = parameter;
                _adapter = adapter;
            }

            public IDescriptor[] Descriptors => new IDescriptor[] { _parameter };

            public object Get(IReadonlyContext context)
            {
                var result = context.TryGet(_parameter, out var val) ? val : _parameter.DefaultValue;
                _parameter.IsValidOrThrow(result);
                if (!(_adapter?.IsValid(_parameter.Field, result) ?? true)) throw new ArgumentException($"Value of field '{_parameter.Field}' is invalid");
                return result;
            }

        }

        private class ObjectInitializer : IInitializer
        {

            private readonly FieldInfo _field;

            private readonly Type _targetType;

            private readonly IAutoParamAdapter _adapter;

            private readonly IDictionary<FieldInfo, IInitializer> _fieldInitializerDict;

            internal ObjectInitializer(FieldInfo field, Type targetType, IDictionary<FieldInfo, IInitializer> fieldInitializerDict, IAutoParamAdapter adapter)
            {
                _field = field;
                _targetType = targetType;
                _fieldInitializerDict = fieldInitializerDict;
                _adapter = adapter;
            }

            [SuppressMessage("ReSharper", "ConvertIfStatementToSwitchStatement")]
            [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
            public IDescriptor[] Descriptors
            {
                get
                {
                    var list = new LinkedList<IDescriptor>();
                    foreach (var pair in _fieldInitializerDict)
                    {
                        var subDescriptors = pair.Value.Descriptors;
                        if (subDescriptors.Length == 0) continue;
                        list.AddLast(subDescriptors.Length == 1 ? subDescriptors.First() : new ParameterGroup(pair.Key.Name, subDescriptors.ToArray()));
                    }
                    return list.ToArray();
                }
            }

            public object Get(IReadonlyContext context)
            {
                var instance = Activator.CreateInstance(_targetType);
                foreach (var pair in _fieldInitializerDict) pair.Key.SetValue(instance, pair.Value.Get(context));
                if (!(_adapter?.IsValid(_field, instance) ?? true)) throw new ArgumentException($"Value of field '{_field}' is invalid");
                return instance;
            }

        }


        private readonly IDictionary<Type, IGroupDescriptor[]> _descriptorsDict = new Dictionary<Type, IGroupDescriptor[]>();

        private readonly IDictionary<Type, IInitializer> _initializerDict = new Dictionary<Type, IInitializer>();

        private static IInitializer GetInitializer([CanBeNull] object instance, [CanBeNull] FieldInfo field, [CanBeNull] AutoParamAttribute attr, [NotNull] Type type)
        {
            if (type == typeof(object)) return NullInitializer.Instance;
            var adapter = attr?.Adapter;
            if (field != null && attr != null && (type.IsEnum || type.IsPrimitive
                                                              || type == typeof(string)
                                                              || type == typeof(System.Drawing.Color) || type == typeof(System.Drawing.Color?)
                                                              || type == typeof(System.Windows.Media.Color) || type == typeof(System.Windows.Media.Color?)))
                return new ValueInitializer(new AutoParameter(field, attr, instance), adapter);
            var dict = new Dictionary<FieldInfo, IInitializer>();
            foreach (var fieldInfo in type.GetFields())
            {
                var configurable = fieldInfo.GetCustomAttribute<AutoParamAttribute>();
                if (configurable == null) continue;
                var fieldValue = fieldInfo.GetValue(instance) ?? (type == typeof(string) ? "" : Activator.CreateInstance(fieldInfo.FieldType));
                var initializer = GetInitializer(fieldValue, fieldInfo, configurable, fieldInfo.FieldType);
                dict[fieldInfo] = initializer;
            }
            return new ObjectInitializer(field, type, dict, adapter);
        }

        [SuppressMessage("ReSharper", "ConvertIfStatementToSwitchStatement")]
        [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
        private static IGroupDescriptor[] Simplify(IGroupDescriptor groupDescriptor)
        {
            var itemCount = groupDescriptor.Items.Count;
            if (itemCount == 0) return EmptyArray<IGroupDescriptor>.Instance;
            // ReSharper disable once TailRecursiveCall
            if (itemCount == 1 && groupDescriptor.Items.First() is IGroupDescriptor group) return Simplify(group);
            if (groupDescriptor.Items.All(item => item is IGroupDescriptor)) return groupDescriptor.Items.OfType<IGroupDescriptor>().ToArray();
            return new[] { groupDescriptor };
        }

        public override IReadOnlyCollection<IGroupDescriptor> GetParameterGroups(Type paradigmClz) => GetIDescriptors(paradigmClz);

        public override IParadigm Create(Type paradigmClz, IReadonlyContext context) => (IParadigm)GetInitializer(paradigmClz).Get(context);

        private IGroupDescriptor[] GetIDescriptors(Type type)
        {
            if (_descriptorsDict.TryGetValue(type, out var cached)) return cached;
            return _descriptorsDict[type] = Simplify(new ParameterGroup(GetInitializer(type).Descriptors));
        }

        private IInitializer GetInitializer(Type type)
        {
            if (_initializerDict.TryGetValue(type, out var cached)) return cached;
            return _initializerDict[type] = GetInitializer(Activator.CreateInstance(type), null, null, type);
        }

    }

}
