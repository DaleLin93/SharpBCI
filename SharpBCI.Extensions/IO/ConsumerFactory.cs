﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.Windows;

namespace SharpBCI.Extensions.IO
{

    /// <summary>
    /// Factory of Consumer.
    /// </summary>
    public interface IConsumerFactory
    {

        [NotNull] Type GetAcceptType(Type consumerClass);

        [NotNull] IReadOnlyCollection<IParameterDescriptor> GetParameters(Type consumerClass);

        [NotNull] IConsumer Create(Type consumerClass, Session session, IReadonlyContext context, byte? num);

    }

    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
    public sealed class ConsumerAttribute : Attribute
    {

        public ConsumerAttribute([NotNull] string name, [NotNull] Type factoryType, [CanBeNull] string version = null, [CanBeNull] string versionName = null)
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

    /// <summary>
    /// An abstract implementation IConsumerFactory.
    /// </summary>
    /// <typeparam name="T">Accept type of Consumer </typeparam>
    public abstract class ConsumerFactory<T> : IConsumerFactory, IParameterPresentAdapter
    {

        protected ConsumerFactory(params IParameterDescriptor[] parameters) => Parameters = parameters;

        public Type AcceptType => typeof(T);

        public virtual double DesiredWidth => double.NaN;

        public virtual bool CanReset(IParameterDescriptor parameter) => true;

        public bool CanCollapse(IGroupDescriptor @group, int depth) => true;

        public virtual bool IsEnabled(IReadonlyContext context, IParameterDescriptor parameter) => true;

        public virtual bool IsVisible(IReadonlyContext context, IDescriptor descriptor) => true;

        public virtual IReadOnlyCollection<IParameterDescriptor> Parameters { get; }

        public abstract IConsumer<T> Create(Session session, IReadonlyContext context, byte? num);

        Type IConsumerFactory.GetAcceptType(Type consumerClass) => AcceptType;

        IReadOnlyCollection<IParameterDescriptor> IConsumerFactory.GetParameters(Type consumerClass) => Parameters;

        IConsumer IConsumerFactory.Create(Type consumerClass, Session session, IReadonlyContext context, byte? num) => Create(session, context, num);

    }

}
