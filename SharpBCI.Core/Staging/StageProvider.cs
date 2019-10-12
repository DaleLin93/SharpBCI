using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using JetBrains.Annotations;

namespace SharpBCI.Core.Staging
{

    public interface IStageProvider
    {

        bool IsPreloadable { get; }

        bool IsBreakable { get; }

        bool IsBroken { get; }

        IStageProvider Preloaded();

        void Break();

        /// <summary>
        /// Get next stage.
        /// </summary>
        /// <returns>Stage or null for end</returns>
        [CanBeNull] Stage Next();

    }

    public sealed class EmptyStageProvider : IStageProvider
    {

        public static readonly EmptyStageProvider Instance = new EmptyStageProvider();

        private EmptyStageProvider() { }

        public bool IsPreloadable => true;

        public bool IsBreakable => true;

        public bool IsBroken => false;

        public IStageProvider Preloaded() => this;

        public void Break() { }

        public Stage Next() => null;

    }

    public class DelegatedStageProvider : IStageProvider
    {

        protected readonly IStageProvider StageProvider;

        public DelegatedStageProvider(IStageProvider stageProvider) => StageProvider = stageProvider;

        public virtual bool IsPreloadable => StageProvider?.IsPreloadable ?? true;

        public virtual bool IsBreakable => true;

        public virtual bool IsBroken { get; private set; }

        public IStageProvider Preloaded() => StageProvider == null ? EmptyStageProvider.Instance : StageProvider.TryPreload(out var preloaded) ? preloaded : StageProvider;

        public void Break() => IsBroken = true;

        public Stage Next() => IsBroken ? null : StageProvider?.Next();

    }

    public class ConditionStageProvider : DelegatedStageProvider
    {

        public ConditionStageProvider(bool val, IStageProvider trueStageProvider) : base(val ? trueStageProvider : null) { }

        public ConditionStageProvider(bool val, IStageProvider falseStageProvider, IStageProvider trueStageProvider) : base(val ? trueStageProvider : falseStageProvider) { }

    }

    /// <summary>
    /// A collection-based stage provider.
    /// </summary>
    public class StageProvider : IStageProvider
    {

        private readonly ICollection<Stage> _stages;

        private IEnumerator<Stage> _enumerator;

        public StageProvider(params Stage[] stages) : this((ICollection<Stage>)stages) { }

        public StageProvider(ICollection<Stage> enumerable)
        {
            _stages = enumerable;
            _enumerator = enumerable.GetEnumerator();
        }

        public static StageProvider Preload(IStageProvider provider)
        {
            if (provider is StageProvider csp) return csp;
            return new StageProvider(provider.GetStages());
        }

        public int Count => _stages.Count;

        public bool IsPreloadable => true;

        public bool IsBreakable => true;

        public bool IsBroken => false;

        public TimeSpan TotalDuration => _stages.GetDuration();

        public ulong TotalDurationInMillis => _stages.GetDurationInMillis();

        public IStageProvider Preloaded() => this;

        public void Break() => _enumerator = null;

        public Stage Next()
        {
            while (_enumerator != null && _enumerator.MoveNext())
                if (_enumerator.Current != null)
                    return _enumerator.Current;
            return null;
        }

    }

    public class CompositeStageProvider : IStageProvider
    {

        private readonly IList<IStageProvider> _providers;

        private int _nextProviderIndex = 0;

        private IStageProvider _provider;

        public CompositeStageProvider(params IStageProvider[] array) : this((IEnumerable<IStageProvider>)array) { }

        public CompositeStageProvider(IEnumerable<IStageProvider> enumerable) => _providers = enumerable.ToList();

        public IStageProvider this[int index] => _providers[index];

        public IStageProvider CurrentProvider
        {
            get
            {
                for (;;)
                {
                    if (_provider != null && !ReferenceEquals(_provider, EmptyStageProvider.Instance)) return _provider;
                    if (_nextProviderIndex >= _providers.Count) return null;
                    _provider = _providers[_nextProviderIndex++];
                }
            }
        }

        public IEnumerable<IStageProvider> Providers => new ReadOnlyCollection<IStageProvider>(_providers);

        public virtual bool IsPreloadable => Providers.All(p => p.IsPreloadable);

        public virtual bool IsBreakable => false;

        public virtual bool IsBroken => false;

        public void Preload()
        {
            for (var i = 0; i < _providers.Count; i++)
                _providers[i] = _providers[i]?.GetPreloadedOrSelf();
        }

        public virtual IStageProvider Preloaded()
        {
            if (GetType() == typeof(CompositeStageProvider) && IsPreloadable) return StageProvider.Preload(this);
            Preload();
            return this;
        }

        public void Break() => throw new NotSupportedException();

        public Stage Next()
        {
            for (;;)
            {
                var p = CurrentProvider;
                if (p == null) return null;
                var s = p.Next();
                if (s != null) return s;
                _provider = null;
            }
        }

    }

    public static class StageProviderUtils
    {

        public static IEnumerable<Stage> AsEnumerable(this IStageProvider stageProvider)
        {
            for (;;)
            {
                var next = stageProvider.Next();
                if (next == null) yield break;
                yield return next;
            }
        }

        public static TimeSpan GetDuration(this IEnumerable<Stage> stages) => TimeSpan.FromMilliseconds(GetDurationInMillis(stages));

        public static ulong GetDurationInMillis(this IEnumerable<Stage> stages) => stages.Aggregate<Stage, ulong>(0, (current, stage) => current + stage.Duration);

        public static ICollection<Stage> GetStages(this IStageProvider provider) => GetStages(new[] { provider });

        public static ICollection<Stage> GetStages(this IEnumerable<IStageProvider> providers)
        {
            var stages = new LinkedList<Stage>();
            foreach (var stageProvider in providers)
            {
                for (;;)
                {
                    var stage = stageProvider.Next();
                    if (stage == null)
                        break;
                    stages.AddLast(stage);
                }
            }
            return stages;
        }

        public static IStageProvider GetPreloadedOrSelf(this IStageProvider provider) => TryPreload(provider, out var preloaded) ? preloaded : provider; 

        /// <summary>
        /// Try preload given stage provider
        /// </summary>
        /// <param name="provider"></param>
        /// <param name="preloaded"></param>
        /// <returns></returns>
        [SuppressMessage("ReSharper", "ConvertIfStatementToSwitchStatement")]
        public static bool TryPreload(this IStageProvider provider, out IStageProvider preloaded)
        {
            if (provider.IsPreloadable)
            {
                preloaded = provider.Preloaded();
                return true;
            }
            preloaded = null;
            return false;
        }

    }

}
