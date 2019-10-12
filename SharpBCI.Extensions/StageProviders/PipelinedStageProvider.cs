using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.StageProviders
{

    public class PipelinedStageProvider : IStageProvider
    {

        private readonly object _consumeLock = new object();

        private readonly Semaphore _stageSemaphore;

        private readonly TimeSpan _waitPeriod;

        private readonly LinkedList<Stage> _stages;

        private int _ended;

        public PipelinedStageProvider(int maxBufferedSize, TimeSpan waitPeriod)
        {
            _stageSemaphore = new Semaphore(0, maxBufferedSize);
            _stages = new LinkedList<Stage>();
            _waitPeriod = waitPeriod;
        }

        public bool IsPreloadable => false;

        public bool IsBreakable => true;

        public bool IsBroken => Interlocked.CompareExchange(ref _ended, 0, 0) != 0;

        public void Offer(params Stage[] stages) => Offer((IReadOnlyCollection<Stage>) stages);

        public void Offer(IReadOnlyCollection<Stage> stages)
        {
            if (stages == null || stages.Count == 0) throw new ArgumentException("stages cannot be null or empty");
            if (stages.Any(Predicates.IsNull)) throw new ArgumentException("stage cannot be null");
            if (IsBroken) throw new StateException("current stage provider is already broken");
            lock (_stages)
            {
                foreach (var stage in stages)
                    if (stage != null)
                    {
                        _stages.AddLast(stage);
                        _stageSemaphore.Release();
                    }
            }
        }

        public IStageProvider Preloaded() => throw new NotSupportedException();

        public void Break()
        {
            if (Interlocked.CompareExchange(ref _ended, 1, 0) == 0)
            {
                lock (_stages)
                    _stages.AddLast((Stage)null);
                _stageSemaphore.Release();
            }
        }

        public Stage Next()
        {
            lock (_consumeLock)
            {
                for (;;)
                {
                    if (IsBroken)
                        lock (_stages)
                            if (_stages.IsEmpty())
                                return null;
                    if (!_stageSemaphore.WaitOne(_waitPeriod)) continue;
                    lock (_stages)
                    {
                        var stage = _stages.First.Value;
                        _stages.RemoveFirst();
                        OnStagePolled(stage);
                        return stage;
                    }
                }
            }
        }

        protected virtual void OnStagePolled(Stage stage) { }

    }
}
