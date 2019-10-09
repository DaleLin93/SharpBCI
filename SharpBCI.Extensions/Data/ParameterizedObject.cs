using System;
using System.Collections.Generic;
using System.Reflection;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;

namespace SharpBCI.Extensions.Data
{

    /// <summary>
    /// Parameterized object interface, ParameterizedObjectAttribute must be declared in subclass.
    /// </summary>
    public interface IParameterizedObject { }

    public interface IParameterizedObjectFactory
    {

        IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter);

        bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter);

        IParameterizedObject Create(IParameterDescriptor parameter, IReadonlyContext context);

        IReadonlyContext Parse(IParameterDescriptor parameter, IParameterizedObject parameterizedObject);

    }

    public abstract class ParameterizedObjectFactory<T> : IParameterizedObjectFactory where T : IParameterizedObject
    {

        public virtual IReadOnlyCollection<IParameterDescriptor> GetParameters(IParameterDescriptor parameter) =>
            GetType().ReadStaticFields<IParameterDescriptor>().AsReadonly();

        public virtual bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => true;

        public abstract T Create(IParameterDescriptor parameter, IReadonlyContext context);

        public abstract IReadonlyContext Parse(IParameterDescriptor parameter, T value);

        IParameterizedObject IParameterizedObjectFactory.Create(IParameterDescriptor parameter, IReadonlyContext context) => 
            Create(parameter, context);

        IReadonlyContext IParameterizedObjectFactory.Parse(IParameterDescriptor parameter, IParameterizedObject parameterizedObject) =>
            Parse(parameter, (T) parameterizedObject);

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Interface)]
    public sealed class ParameterizedObjectAttribute : Attribute
    {

        private static readonly IDictionary<Type, IParameterizedObjectFactory> Factories = new Dictionary<Type, IParameterizedObjectFactory>();

        public ParameterizedObjectAttribute(Type factoryType) => Factory = Initiate(factoryType);

        private static IParameterizedObjectFactory Initiate(Type factoryType)
        {
            IParameterizedObjectFactory factory;
            lock (Factories)
                if (!Factories.TryGetValue(factoryType, out factory))
                {
                    if (!typeof(IParameterizedObjectFactory).IsAssignableFrom(factoryType))
                        throw new ArgumentException("'factoryType' must implements IParameterizedObjectFactory");
                    Factories[factoryType] = factory = (IParameterizedObjectFactory) Activator.CreateInstance(factoryType);
                }
            return factory;
        }

        public IParameterizedObjectFactory Factory { get; }

    }

    public static class ParameterizedObjectExt
    {

        public static readonly ContextProperty<IParameterizedObjectFactory> FactoryProperty = new ContextProperty<IParameterizedObjectFactory>();

        public static readonly ContextProperty<bool> PopupProperty = new ContextProperty<bool>(false);

        public static ParameterizedObjectAttribute GetParameterizedObjectAttribute(this Type type)
        {
            if (!typeof(IParameterizedObject).IsAssignableFrom(type))
                throw new ArgumentException($"Given type '{type.FullName}' must implements interface IParameterizedObject");
            var attribute = type.GetCustomAttribute<ParameterizedObjectAttribute>(true);
            if (attribute != null) return attribute;
            foreach (var @interface in type.GetInterfaces())
            {
                attribute = @interface.GetCustomAttribute<ParameterizedObjectAttribute>();
                if (attribute != null) return attribute;
            }
            throw new ProgrammingException($"ParameterizedObjectAttribute must be declared while implementing IParameterizedObject interface, type: '{type.FullName}'");
        }

        public static IParameterizedObjectFactory GetParameterizedObjectFactory(this Type type) => GetParameterizedObjectAttribute(type).Factory;

        public static IParameterizedObjectFactory GetParameterizedObjectFactory(this IParameterDescriptor parameter) =>
            FactoryProperty.TryGet(parameter.Metadata, out var factory) ? factory : GetParameterizedObjectFactory(parameter.ValueType);

    }

}
