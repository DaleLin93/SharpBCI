using System;
using System.Collections.Generic;
using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.StageProviders
{

    public abstract class RepeatingStageProvider : IStageProvider
    {

        public class Static : RepeatingStageProvider
        {

            private new readonly IEnumerable<Stage> _stages;

            public Static(Stage stage, uint count) : base(count) => _stages = new[] { stage };

            public Static(IEnumerable<Stage> stages, uint count) : base(count) => _stages = stages;

            public static Static Unlimited(params Stage[] stages) => Unlimited((IEnumerable<Stage>) stages);

            public static Static Unlimited(IEnumerable<Stage> stages) => new Static(stages, UnlimitedCount);

            public sealed override bool IsDynamic => IsUnlimited;

            protected override IEnumerable<Stage> GetStages(uint index) => _stages;

        }

        public const uint UnlimitedCount = 0;

        private uint _nextIndex;

        private IEnumerator<Stage> _stages;

        protected RepeatingStageProvider(uint count) => Count = count;

        public uint Count { get; }

        public bool IsUnlimited => UnlimitedCount == Count;

        public abstract bool IsDynamic { get; }

        public virtual bool IsPreloadable => !IsUnlimited && !IsDynamic;

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
