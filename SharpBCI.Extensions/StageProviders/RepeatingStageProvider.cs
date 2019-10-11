using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using MarukoLib.Lang;
using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.StageProviders
{

    public abstract class RepeatingStageProvider : IStageProvider
    {

        public class Simple : RepeatingStageProvider
        {

            private readonly IEnumerable<Stage> _repeatingStages;

            public Simple(Stage stage, uint count)
                : base(count) => _repeatingStages = new[] { stage };

            public Simple(IEnumerable<Stage> stages, uint count)
                : base(count) => _repeatingStages = stages;

            public Simple(EventWaitHandle eventWaitHandle, IEnumerable<Stage> stages, uint count)
                : base(eventWaitHandle, count) => _repeatingStages = stages;

            public static Simple Unlimited(params Stage[] stages) => Unlimited((IEnumerable<Stage>)stages);

            public static Simple Unlimited(IEnumerable<Stage> stages) => new Simple(stages, UnlimitedCount);

            public static Simple Unlimited(EventWaitHandle eventWaitHandle, params Stage[] stages) => 
                Unlimited(eventWaitHandle, (IEnumerable<Stage>)stages);

            public static Simple Unlimited(EventWaitHandle eventWaitHandle, IEnumerable<Stage> stages) =>
                new Simple(eventWaitHandle, stages, UnlimitedCount);

            public sealed override bool IsDynamic => IsUnlimited;

            protected override IEnumerable<Stage> GetStages(uint index) => _repeatingStages;

        }

        public class Advanced : RepeatingStageProvider
        {

            private readonly bool _preloadable;

            private readonly Func<uint, IEnumerable<Stage>> _stageProvidingFunc;

            public Advanced(Func<uint, IStageProvider> stageProviderFunc, uint count, bool preloadable = true)
                : this(idx => stageProviderFunc(idx)?.AsEnumerable() ?? EmptyArray<Stage>.Instance, count, preloadable) { }

            public Advanced(Func<uint, IEnumerable<Stage>> stageProvidingFunc, uint count, bool preloadable = true)
                : this(null, stageProvidingFunc, count, preloadable) { } 

            public Advanced(EventWaitHandle eventWaitHandle, Func<uint, IEnumerable<Stage>> stageProvidingFunc, uint count, bool preloadable = true)
                : base(eventWaitHandle, count)
            {
                _stageProvidingFunc = stageProvidingFunc;
                _preloadable = preloadable;
            }

            public static Advanced Unlimited(Func<uint, IEnumerable<Stage>> func) => new Advanced(func, UnlimitedCount);

            public static Advanced Unlimited(EventWaitHandle eventWaitHandle, Func<uint, IEnumerable<Stage>> func) =>
                new Advanced(eventWaitHandle, func, UnlimitedCount);

            public sealed override bool IsDynamic => IsUnlimited || !_preloadable;

            protected override IEnumerable<Stage> GetStages(uint index) => _stageProvidingFunc?.Invoke(index);

        }

        public const uint UnlimitedCount = 0;

        private uint _nextIndex;

        private IEnumerator<Stage> _stages;

        protected RepeatingStageProvider(uint count) : this(null, count) { }

        protected RepeatingStageProvider(EventWaitHandle eventWaitHandle, uint count)
        {
            EventWaitHandle = eventWaitHandle;
            Count = count;
        }

        [CanBeNull] 
        public EventWaitHandle EventWaitHandle { get; }

        public uint Count { get; }

        public bool IsUnlimited => UnlimitedCount == Count;

        public abstract bool IsDynamic { get; }

        public virtual bool IsPreloadable => !IsUnlimited && EventWaitHandle == null && !IsDynamic;

        public bool IsBreakable => true;

        public bool IsBroken { get; private set; }

        public IStageProvider Preloaded()
        {
            if (IsPreloadable) return StageProvider.Preload(this);
            throw new NotSupportedException("Infinite or dynamic RepeatingStageProviders are not preloadable");
        }

        public void Break() => IsBroken = true;

        public Stage Next()
        {
            while (!IsBroken)
            {
                if (!(_stages?.MoveNext() ?? false))
                {
                    if (!IsUnlimited && _nextIndex >= Count) return null;
                    if (EventWaitHandle != null) while(!EventWaitHandle.WaitOne()) { }
                    _stages = GetStages(_nextIndex)?.GetEnumerator();
                    _nextIndex++;
                }
                else
                {
                    var s = _stages.Current;
                    if (s != null) return s;
                }
            }
            return null;
        }

        protected abstract IEnumerable<Stage> GetStages(uint index);

    }

}
