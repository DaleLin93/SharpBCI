using System;
using System.Threading;
using SharpBCI.Core.Staging;

namespace SharpBCI.Extensions.StageProviders
{

    public class EventWaitingStageProvider : IStageProvider
    {

        private readonly TimeSpan? _brokenCheckingPeriod;

        public EventWaitingStageProvider(EventWaitHandle eventWaitHandle, long waitingPeriod) 
            : this(eventWaitHandle, TimeSpan.FromMilliseconds(waitingPeriod)) { }

        public EventWaitingStageProvider(EventWaitHandle eventWaitHandle, TimeSpan? brokenCheckingPeriod = null)
        {
            EventWaitHandle = eventWaitHandle;
            _brokenCheckingPeriod = brokenCheckingPeriod;
        }

        public EventWaitHandle EventWaitHandle { get; }

        public bool IsPreloadable => false;

        public bool IsBreakable => _brokenCheckingPeriod != null;

        public bool IsBroken { get; private set; }

        public IStageProvider Preloaded() => throw new NotSupportedException();

        public void Break()
        {
            if (IsBreakable) IsBroken = true;
            else throw new NotSupportedException("'brokenCheckingPeriod' must be set to break.");
        }

        public Stage Next()
        {
            if (_brokenCheckingPeriod == null)
                EventWaitHandle?.WaitOne();
            else
                while (!IsBroken && !(EventWaitHandle?.WaitOne(_brokenCheckingPeriod.Value) ?? true)) { }
            return null;
        }
        
    }

}
