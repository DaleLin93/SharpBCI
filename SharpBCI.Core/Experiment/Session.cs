using System;
using System.IO;
using System.Windows.Threading;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;
using MarukoLib.UI;
using Newtonsoft.Json;
using SharpBCI.Core.IO;

namespace SharpBCI.Core.Experiment
{
    
    public sealed class SessionClock : AlignedClock
    {

        public SessionClock(IClock clock) : base(clock, -clock.Time) => CreateTime = -Offset;

        public long CreateTime { get; }

    }

    public enum SessionState
    {
        Initialized, Started, Stopped
    }

    public sealed class Session : ContextObject
    {

        public delegate void SessionEventHandler<in T>(Session session, T evt);

        private static readonly object SessionInstanceLock = new object();

        private static volatile Session _session;

        [JsonIgnore] public readonly Random R = new Random();

        public event SessionEventHandler<EventArgs> Started;

        public event SessionEventHandler<EventArgs> Finished;

        public Session(Dispatcher dispatcher, string subject, string descriptor, IClock clock, 
            IParadigm paradigm, StreamerCollection streamerCollection, string dataFolder)
        {
            Dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            Subject = subject.Trim2Null() ?? throw new ArgumentException("'subject' cannot be blank");
            Descriptor = descriptor.Trim2Null() ?? throw new ArgumentException("'descriptor' cannot be blank");
            Clock = new SessionClock(clock ?? throw new ArgumentNullException(nameof(clock)));
            Paradigm = paradigm ?? throw new ArgumentNullException(nameof(paradigm));
            StreamerCollection = streamerCollection ?? throw new ArgumentNullException(nameof(streamerCollection));

            Screens = ScreenInfo.All;
            DataFilePrefix = Path.Combine(dataFolder, GetFullSessionName(CreateTimestamp, subject, descriptor));
        }

        /// <summary>
        /// Whether it has a running session or not.
        /// </summary>
        public static bool HasRunningSession => _session != null;

        /// <summary>
        /// Get the currently running session.
        /// </summary>
        [NotNull] public static Session Current => _session ?? throw new StateException("No available session now");

        /// <summary>
        /// Time is not present: 
        ///     Format: baseFolder\subject-descriptor
        /// Else
        ///     Format: baseFolder\time-subject-descriptor
        /// </summary>
        public static string GetFullSessionName(long? time, string subject, string descriptor) => 
            (time == null ? $"{subject}-{descriptor}" : $"{time}-{subject}-{descriptor}").RemoveInvalidCharacterForFileName();

        private static void StartSession(Session session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            lock (SessionInstanceLock)
            {
                if (_session != null) throw new StateException();
                _session = session;
            }
        }

        private static void CloseSession(Session session)
        {
            if (session == null) throw new ArgumentNullException(nameof(session));
            lock (SessionInstanceLock)
            {
                if (_session != session) throw new StateException();
                _session = null;
            }
        }

        /// <summary>
        /// STA dispatcher.
        /// </summary>
        [NotNull] [JsonIgnore] public Dispatcher Dispatcher { get; }

        [NotNull] public string Subject { get; }

        [NotNull] public string Descriptor { get; }

        /// <summary>
        /// Format: 'baseFolder\time-subject-descriptor' (with out extension)
        /// </summary>
        [NotNull] public string DataFilePrefix { get; }

        /// <summary>
        /// Session clock, t=0 while it's just created.
        /// </summary>
        [NotNull] public SessionClock Clock { get; }

        /// <summary>
        /// (Absolute time) Timestamp of session initialization.
        /// </summary>
        public long CreateTimestamp => Clock.Unit.ConvertTo(Clock.CreateTime, TimeUnit.Millisecond);

        /// <summary>
        /// (Relative time) Session start time relative to create time.
        /// </summary>
        public long StartTime { get; private set; } = -1;

        /// <summary>
        /// (Relative time) Session end time relative to create time.
        /// </summary>
        public long EndTime { get; private set; } = -1;

        /// <summary>
        /// (Relative time) Current time relative to create time.
        /// </summary>
        public ulong SessionTime => (ulong)Clock.GetMilliseconds();

        public SessionState State => StartTime == -1 ? SessionState.Initialized
            : (EndTime == -1 ? SessionState.Started : SessionState.Stopped);

        /// <summary>
        /// Screen parameters.
        /// </summary>
        [NotNull] public ScreenInfo[] Screens { get; }

        /// <summary>
        /// The paradigm of current session.
        /// </summary>
        [NotNull] public IParadigm Paradigm { get; }

        [NotNull] public StreamerCollection StreamerCollection { get; }

        /// <summary>
        /// The paradigm result of current session, always null if the session is not finished.
        /// </summary>
        [CanBeNull] public Result Result { get; private set; }

        /// <summary>
        /// Whether the session is stopped by the user manually or not.
        /// </summary>
        public bool UserInterrupted { get; private set; } = false;

        /// <summary>
        /// Get data file name by given file extension.
        /// </summary>
        /// <param name="extension">file extension, e.g. '.exe' or 'exe'</param>
        /// <param name="num">file order num</param>
        /// <returns>Full path of target file</returns>
        public string GetDataFileName(string extension, byte? num = null)
        {
            var filePathWithoutExt = num == null ? DataFilePrefix : $"{DataFilePrefix}#{num}";
            if ((extension = extension?.Trim())?.IsEmpty() ?? true) return $"{filePathWithoutExt}";
            return extension.StartsWith(".") ? $"{filePathWithoutExt}{extension}" : $"{filePathWithoutExt}.{extension}";
        }

        /// <summary>
        /// Run the session and get the result.
        /// </summary>
        /// <returns>Result of session.</returns>
        [CanBeNull]
        public Result Run() 
        {
            Paradigm.Run(this);
            lock (this)
                if (EndTime == -1)
                    throw new StateException("Paradigm did not finish correctly");
            return Result;
        }

        public void Start()
        {
            StartSession(this);
            lock (this)
            {
                if (StartTime != -1) throw new StateException("Session is already started");
                StartTime = Clock.GetMilliseconds();
                Started?.Invoke(this, EventArgs.Empty);
            }
        }

        public void Finish(bool userInterrupted) => Finish(null, userInterrupted);
        
        public void Finish([CanBeNull] Result result, bool userInterrupted)
        {
            CloseSession(this);
            lock (this)
            {
                if (EndTime != -1) throw new StateException("Session is already finished");
                EndTime = Clock.GetMilliseconds();
                Result = result;
                UserInterrupted = userInterrupted;
                Finished?.Invoke(this, EventArgs.Empty);
            }
        }

    }

    public struct SessionInfo
    {

        public const string FileSuffix = ".session";

        public SessionInfo(Session session)
        {
            Subject = session.Subject;
            SessionName = session.Descriptor;
            CreateTime = session.CreateTimestamp;
            StartTime = session.StartTime;
            EndTime = session.EndTime;
            Screens = session.Screens;
            UserInterrupted = session.UserInterrupted;
            DataFilePrefix = session.DataFilePrefix;
        }

        public string Subject { get; set; }

        public string SessionName { get; set; }

        public long CreateTime { get; set; }

        public long StartTime { get; set; } 

        public long EndTime { get; set; }

        public ScreenInfo[] Screens { get; set; }

        public bool UserInterrupted { get; set; }

        public string DataFilePrefix { get; set; }

    }

}
