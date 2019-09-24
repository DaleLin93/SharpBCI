using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;

namespace SharpBCI.Extensions.Experiments
{

    public class AutoExperimentFactory : ExperimentFactory
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

            public IDescriptor[] Descriptors => new IDescriptor[] {_parameter};

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

        public override IReadOnlyCollection<IGroupDescriptor> GetParameterGroups(Type experimentClass) => GetIDescriptors(experimentClass);

        public override IExperiment Create(Type experimentClass, IReadonlyContext context) => (IExperiment) GetInitializer(experimentClass).Get(context);

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
