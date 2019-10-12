using System;
using System.Collections.Generic;
using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.StageProviders
{

    public abstract class SegmentedStageProvider : IStageProvider
    {
        
        private IEnumerator<Stage> _stages;

        protected SegmentedStageProvider(bool preloadable) => IsPreloadable = preloadable;

        public bool IsPreloadable { get; }

        public bool IsBreakable => true;

        public bool IsBroken { get; private set; }

        public virtual IStageProvider Preloaded()
        {
            if (!IsPreloadable) throw new NotSupportedException();
            return StageProvider.Preload(this);
        }

        public void Break() => IsBroken = true;

        public Stage Next()
        {
            for (;;)
            {
                if (_stages == null)
                {
                    _stages = Following()?.GetEnumerator();
                    if(_stages == null)
                        return null;
                }
                if (!_stages.MoveNext())
                    _stages = null;
                else
                {
                    var s = _stages.Current;
                    if (s != null)
                        return s;
                }
            }
        }

        protected abstract IEnumerable<Stage> Following();

    }
}
