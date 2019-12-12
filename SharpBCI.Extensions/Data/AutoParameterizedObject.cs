using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using JetBrains.Annotations;
using MarukoLib.Lang;

namespace SharpBCI.Extensions.Data
{

    public sealed class AutoParameterizedObjectFactory : IParameterizedObjectFactory
    {

        private struct AutoParameterizedObjectMeta
        {

            [NotNull] public readonly Func<IParameterizedObject> Constructor;

            [NotNull] public readonly AutoParameter[] Parameters;

            public AutoParameterizedObjectMeta([NotNull] Func<IParameterizedObject> constructor, [NotNull] AutoParameter[] parameters)
            {
                Constructor = constructor;
                Parameters = parameters;
            }

        }

        private readonly IDictionary<Type, AutoParameterizedObjectMeta> _autoParameters = new Dictionary<Type, AutoParameterizedObjectMeta>();

        // ReSharper disable once SuggestBaseTypeForParameter
        private AutoParameterizedObjectMeta GetMeta(IParameterDescriptor parameter)
        {
            if (_autoParameters.TryGetValue(parameter.ValueType, out var result)) return result;
            if (!typeof(IParameterizedObject).IsAssignableFrom(parameter.ValueType)) throw new ArgumentException("IParameterizedObject interface is required");
            Func<IParameterizedObject> constructor;
            if (parameter.ValueType.IsClass)
            {
                var classConstructor = parameter.ValueType.GetConstructor(EmptyArray<Type>.Instance) ?? throw new ArgumentException("no-arg constructor is required for class");
                constructor = () => (IParameterizedObject)classConstructor.Invoke(EmptyArray<object>.Instance);
            }
            else
                constructor = () => (IParameterizedObject)Activator.CreateInstance(parameter.ValueType);
            var parameters = new LinkedList<AutoParameter>();
            foreach (var field in parameter.ValueType.GetFields())
            {
                if (field.IsStatic || field.IsInitOnly) continue;
                var attribute = field.GetCustomAttribute<AutoParamAttribute>();
                if (attribute == null) continue;
                parameters.AddLast(new AutoParameter(field, attribute));
            }
            return _autoParameters[parameter.ValueType] = new AutoParameterizedObjectMeta(constructor, parameters.ToArray());
        }

        public IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter) => GetMeta(parameter).Parameters.ToArray<IParameterDescriptor>();

        public bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => true;

        public IParameterizedObject Create(IParameterDescriptor parameter, IReadonlyContext context)
        {
            var meta = GetMeta(parameter);
            var value = meta.Constructor();
            foreach (var autoParameter in meta.Parameters) autoParameter.Field.SetValue(value, context.TryGet(autoParameter, out var pv) ? pv : autoParameter.DefaultValue);
            return value;
        }

        public IReadonlyContext Parse(IParameterDescriptor parameter, IParameterizedObject parameterizedObject)
        {
            var meta = GetMeta(parameter);
            var context = new Context(meta.Parameters.Length);
            foreach (var autoParameter in meta.Parameters) context[autoParameter] = autoParameter.Field.GetValue(parameterizedObject);
            return context;
        }

    }

    [ParameterizedObject(typeof(AutoParameterizedObjectFactory))]
    public interface IAutoParameterizedObject : IParameterizedObject { }

}