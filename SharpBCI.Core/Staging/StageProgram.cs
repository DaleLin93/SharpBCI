using System;
using System.Collections.Generic;
using System.Threading;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Lang.Concurrent;
using MarukoLib.Threading;

namespace SharpBCI.Core.Staging
{

    /// <summary>
    /// Action for new stage.
    /// </summary>
    public enum StageAction
    {
        /// <summary>
        /// Default. Accept current stage.
        /// </summary>
        Accept,
        /// <summary>
        /// Skip current stage.
        /// </summary>
        Skip,
        /// <summary>
        /// Pause at the start of stage.
        /// </summary>
        Pause,
        /// <summary>
        /// Terminate the stage program.
        /// </summary>
        Terminate
    }

    public class StageChangedEventArgs : EventArgs
    {

        internal StageChangedEventArgs(ulong programTime, Stage stage)
        {
            ProgramTime = programTime;
            Stage = stage;
        }

        /// <summary>
        /// (Relative time) Program running time.
        /// </summary>
        public ulong ProgramTime { get; }

        public Stage Stage { get; }

        /// <summary>
        /// Check if the end is reached. (Stage is null)
        /// </summary>
        public bool IsEndReached => Stage == null;

        public StageAction Action { get; set; } = StageAction.Accept;

        public bool TryGetStage(out Stage stage)
        {
            stage = Stage;
            return !IsEndReached;
        }

    }

    /// <summary>
    /// A stage program is a timeline that defined by multiple stages.
    /// </summary>
    public class StageProgram
    {

        /// <summary>
        /// Triggered while the program is started.
        /// </summary>
        public event EventHandler Started;

        /// <summary>
        /// Triggered while the program is stopped.
        /// </summary>
        public event EventHandler Stopped;

        /// <summary>
        /// Triggered while the stage is changed.
        /// </summary>
        public event EventHandler<StageChangedEventArgs> StageChanged;

        /// <summary>
        /// Triggered while the unhandled exception is caught.
        /// </summary>
        public event EventHandler<ExceptionEventArgs> UnhandledException;

        [NotNull] private readonly IStageProvider _stageProvider;

        [NotNull] private readonly AsyncRepeatingRunner _repeatingRunner;

        [NotNull] private readonly AtomicBool _stageSkipped;

        [NotNull] private readonly ManualResetEvent _pausedEvent;

        private ulong _nextUpdateTime;

        /// <summary>
        /// Create a stage program.
        /// </summary>
        /// <param name="clock">Clock for timing</param>
        /// <param name="stages">Stages of program</param>
        public StageProgram(IClock clock, params Stage[] stages) : this(clock, new StageProvider(stages), false) { }

        /// <summary>
        /// Create a stage program.
        /// </summary>
        /// <param name="clock">Clock for timing</param>
        /// <param name="providers">Stage providers to provide stages</param>
        public StageProgram(IClock clock, params IStageProvider[] providers) : this(clock, new CompositeStageProvider(providers)) { }

        /// <summary>
        /// Create a stage program.
        /// </summary>
        /// <param name="clock">Clock for timing</param>
        /// <param name="providers">Stage providers to provide stages</param>
        /// <param name="preferPreloaded">Prefer pre-loaded stages if possible</param>
        public StageProgram(IClock clock, IEnumerable<IStageProvider> providers, bool preferPreloaded = true) : this(clock, new CompositeStageProvider(providers), preferPreloaded) { }

        /// <summary>
        /// Create a stage program.
        /// </summary>
        /// <param name="clock">Clock for timing</param>
        /// <param name="stageProvider">Stage provider that providing stages</param>
        /// <param name="preferPreloaded">Prefer pre-loaded stages if possible</param>
        public StageProgram(IClock clock, IStageProvider stageProvider, bool preferPreloaded = true)
        {
            if (stageProvider == null) throw new ArgumentNullException(nameof(stageProvider));
            OriginalClock = clock ?? throw new ArgumentNullException(nameof(clock));
            FreezableClock = new FreezableClock(clock.As(TimeUnit.Millisecond));
            FreezableClock.Frozen += (sender, e) => _pausedEvent.Set();
            FreezableClock.Unfrozen += (sender, e) => _pausedEvent.Reset();
            _stageProvider = preferPreloaded && stageProvider.TryPreload(out var preloaded) ? preloaded : stageProvider;
            _repeatingRunner = new AsyncRepeatingRunner("StageProgram Worker", DoRun, true, ThreadPriority.Highest, null, StoppingAction.Abort);
            _repeatingRunner.Starting += (sender, e) =>
            {
                CurrentStage = null;
                _pausedEvent.Set();
                StartTime = Time;
                _nextUpdateTime = 0;
            };
            _repeatingRunner.Started += (sender, e) => Started?.Invoke(this, EventArgs.Empty);
            _repeatingRunner.Stopped += (sender, e) => Stopped?.Invoke(this, EventArgs.Empty);
            _stageSkipped = new AtomicBool(false);
            _pausedEvent = new ManualResetEvent(false);
        }

        /// <summary>
        /// The base clock passed in constructor as a parameter.
        /// </summary>
        [NotNull] public IClock OriginalClock { get; }
        
        /// <summary>
        /// The freezable (pausable) clock actually used for stage program (time unit = millisecond).
        /// </summary>
        [NotNull] public FreezableClock FreezableClock { get; }

        /// <summary>
        /// A shorthand of FreezableClock.Time.
        /// </summary>
        public long Time => FreezableClock.Time;

        /// <summary>
        /// (Absolute time) The start time is the absolute time of clock when the program start.
        /// </summary>
        public long StartTime { get; private set; } = 0;

        /// <summary>
        /// (Relative time) Program running time based on start time.
        /// </summary>
        public ulong ProgramTime => (ulong)(Time - StartTime);

        /// <summary>
        /// Whether the stage program is started or not.
        /// </summary>
        public bool IsStarted => _repeatingRunner.IsStarted;

        /// <summary>
        /// Whether the stage program is paused or not.
        /// </summary>
        public bool IsPaused => IsStarted && FreezableClock.IsFrozen;

        public TimeSpan SleepWaitPeriod { get; set; } = TimeSpan.Zero;

        /// <summary>
        /// Current stage.
        /// </summary>
        [CanBeNull] public Stage CurrentStage { get; private set; }

        /// <summary>
        /// Start program.
        /// </summary>
        /// <returns>Successfully started</returns>
        public bool Start() => _repeatingRunner.Start();

        /// <summary>
        /// Stop program.
        /// </summary>
        /// <returns>Successfully stopped</returns>
        public bool Stop()
        {
            if (_repeatingRunner.Stop(false))
            {
                CurrentStage = null;
                FreezableClock.Unfreeze();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Pause program.
        /// </summary>
        /// <returns>Successfully paused</returns>
        public bool Pause() => FreezableClock.Freeze();

        /// <summary>
        /// Resume program.
        /// </summary>
        /// <returns>Successfully resumed</returns>
        public bool Resume() => FreezableClock.Unfreeze();

        /// <summary>
        /// Skip current stage.
        /// </summary>
        /// <returns>Successfully set</returns>
        public bool Skip() => _stageSkipped.Set();

        private void DoRun()
        {
            /* Check frozen. */
            if (FreezableClock.IsFrozen && !_pausedEvent.WaitOne()) return; 

            var now = ProgramTime;

            /* Do sleep waiting while greater than 2 x sleep period */
            var sleepWaitPeriod = SleepWaitPeriod;
            if (sleepWaitPeriod.Ticks > 0) 
                while (!_stageSkipped.Value && FreezableClock.Unit.ConvertTo((long)(_nextUpdateTime - now), TimeUnit.Tick) > sleepWaitPeriod.Ticks * 2)
                {
                    Thread.Sleep(sleepWaitPeriod);
                    now = ProgramTime;
                }

            /* Do busy waiting while less than 2 x sleep period */
            while (now < _nextUpdateTime && !_stageSkipped.Value)
            {
                if (FreezableClock.IsFrozen) return; /* Exit busy waiting while paused */
                now = ProgramTime;
            }

            /* Actual processing procedures */
            do
            {
                /* Reset skip state */
                _stageSkipped.Reset();

                /* Get next stage, exit on null (END REACHED) */
                Stage stage = null;
                try
                {
                    while (true) /* Loop only for skip */
                    {
                        stage = _stageProvider.Next();
                        now = ProgramTime;
                        var eventArgs = new StageChangedEventArgs(now, stage);
                        StageChanged?.Invoke(this, eventArgs);
                        if (stage == null) eventArgs.Action = StageAction.Terminate;
                        switch (eventArgs.Action)
                        {
                            case StageAction.Terminate: /* Terminate entire program */
                                goto StopProgram;
                            case StageAction.Skip: /* Skip current stage directly */
                                continue;
                            case StageAction.Pause: /* Pause at the start of the stage */
                                Pause();
                                goto BreakOuterLoop;
                            default: /* Accepted */
                                goto BreakOuterLoop;
                        }

                        BreakOuterLoop:
                        break;
                    }
                }
                catch (ThreadAbortException) { throw; } /* Aborted by stopping underlying runner */
                catch (Exception e) /* Process unhandled exception */
                {
                    var eventArgs = new ExceptionEventArgs(e);
                    UnhandledException?.Invoke(this, eventArgs);
                    if (!eventArgs.Handled) throw new Exception("Unhandled exception", e);
                }

                /* Update time */
                if (stage != null) _nextUpdateTime = now + stage.Duration;
                continue;

                /* Stop */
                StopProgram:
                Stop();
                return;
            } while (now >= _nextUpdateTime || _stageSkipped.Value); /* Check if need to move next */
        }

    }

}
