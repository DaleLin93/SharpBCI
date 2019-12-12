using JetBrains.Annotations;
using MarukoLib.Lang;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using MarukoLib.Threading;

namespace SharpBCI.Core.IO
{

    public enum StreamerState
    {
        Initialized = 0, Started = 1, Stopping = 2, Stopped = 3
    }

    public interface IStreamer
    {

        [NotNull] string StreamId { get; }

        [NotNull] Type ValueType { get; }

        int FilterCount { get; }

        int ConsumerCount { get; }

        StreamerState State { get; }

        void Start();

        void Stop();

        void AttachFilter([NotNull] IFilter filter);

        bool DetachFilter([NotNull] IFilter filter);

        [NotNull] IEnumerable<T> QueryFilters<T>();

        void AttachConsumer([NotNull] IConsumer consumer);

        bool DetachConsumer([NotNull] IConsumer consumer);

        [NotNull] IEnumerable<T> QueryConsumers<T>();

    }

    public abstract class Streamer : IStreamer
    {

        private readonly LinkedList<IFilter> _filters = new LinkedList<IFilter>();

        private readonly LinkedList<IConsumer> _consumers = new LinkedList<IConsumer>();

        private readonly ReaderWriterLockSlim _filtersLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        private readonly ReaderWriterLockSlim _consumersLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        protected Streamer([NotNull] string streamId, [NotNull] Type valueType)
        {
            StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        }

        private static bool Insert<T>(LinkedList<T> list, T value) where T : IPriority
        {
            if (!list.IsEmpty())
            {
                if (list.Contains(value)) return false;
                var priority = value.Priority;
                var node = list.First;
                while (node != null)
                {
                    if (node.Value.Priority > priority)
                    {
                        list.AddBefore(node, value);
                        return true;
                    }
                    node = node.Next;
                }
            }
            list.AddLast(value);
            return true;
        }

        public string StreamId { get; }

        public Type ValueType { get; }

        public int FilterCount => _filters.Count;

        public int ConsumerCount => _consumers.Count;

        public abstract StreamerState State { get; }

        public abstract void Start();

        public abstract void Stop();

        public void AttachFilter(IFilter filter)
        {
            if (!filter.AcceptType.IsAssignableFrom(ValueType))
                throw new ArgumentException($"Type not match, streamer value type: {ValueType}, filter accept type: {filter.AcceptType}");
            using (_filtersLock.AcquireWriteLock())
                if (!Insert(_filters, filter))
                    throw new ArgumentException("The given filter is already attached");
        }

        public bool DetachFilter(IFilter filter)
        {
            if (!filter.AcceptType.IsAssignableFrom(ValueType)) return false;
            using (_filtersLock.AcquireWriteLock())
                return _filters.Remove(filter);
        }

        public IEnumerable<TFilter> QueryFilters<TFilter>()
        {
            var result = new LinkedList<TFilter>();
            using (_filtersLock.AcquireReadLock())
                foreach (var filter in _filters)
                    if (filter is TFilter tFilter)
                        result.AddLast(tFilter);
            return result;
        }

        public void AttachConsumer(IConsumer consumer)
        {
            if (!consumer.AcceptType.IsAssignableFrom(ValueType)) 
                throw new ArgumentException($"Type not match, streamer value type: {ValueType}, consumer accept type: {consumer.AcceptType}");
            using (_consumersLock.AcquireWriteLock())
                if (!Insert(_consumers, consumer))
                    throw new ArgumentException("The given consumer is already attached");
        }

        public bool DetachConsumer(IConsumer consumer)
        {
            if (!consumer.AcceptType.IsAssignableFrom(ValueType)) return false;
            using (_consumersLock.AcquireWriteLock())
                return _consumers.Remove(consumer);
        }

        public IEnumerable<TConsumer> QueryConsumers<TConsumer>()
        {
            var result = new LinkedList<TConsumer>();
            using (_consumersLock.AcquireReadLock())
                foreach (var consumer in _consumers)
                    if (consumer is TConsumer tConsumer)
                        result.AddLast(tConsumer);
            return result;
        }

        protected bool Accept(object value)
        {
            try
            {
                _filtersLock.EnterWriteLock();
                // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
                foreach (var filter in _filters)
                    if (!filter.Accept(value))
                        return false;
                return true;
            }
            finally
            {
                _filtersLock.ExitWriteLock();
            }
        }

        protected void Dispatch(object value)
        {
            try
            {
                _consumersLock.EnterWriteLock();
                foreach (var consumer in _consumers)
                    consumer.Accept(value);
            }
            finally
            {
                _consumersLock.ExitWriteLock();
            }
        }

    }

    public abstract class Streamer<T> : Streamer
    {

        protected Streamer([NotNull] string streamId) : base(streamId, typeof(T)) { }

    }

    [SuppressMessage("ReSharper", "NotAccessedField.Local")]
    public abstract class AsyncStreamer<T> : Streamer<T>
    {

        public event EventHandler Started;

        public event EventHandler Stopping;

        public event EventHandler Stopped;

        private readonly object _stateLock = new object();

        private readonly LinkedList<T> _queued = new LinkedList<T>();

        private Semaphore _semaphore;

        private volatile StreamerState _state = StreamerState.Initialized;

        private Thread _tProducer, _tConsumer;

        protected AsyncStreamer([NotNull] string streamId) : base(streamId) { }

        public override StreamerState State => _state;

        public sealed override void Start()
        {
            lock (_stateLock)
            {
                if (_state != StreamerState.Initialized)
                    throw new Exception($"Illegal State: {_state}");
                _state = StreamerState.Started;
                _queued.Clear();
                _semaphore = new Semaphore(0, int.MaxValue);
                Started?.Invoke(this, EventArgs.Empty);
            }
            (_tProducer = new Thread(AsyncProducer) { IsBackground = true, Name = $"Stream '{StreamId}' Producer", Priority = ThreadPriority.AboveNormal }).Start();
            (_tConsumer = new Thread(AsyncConsumer) { IsBackground = true, Name = $"Stream '{StreamId}' Consumer", Priority = ThreadPriority.BelowNormal }).Start();
        }

        public sealed override void Stop()
        {
            lock (_stateLock)
                if (_state == StreamerState.Started)
                {
                    Stopping?.Invoke(this, EventArgs.Empty);
                    if (_tProducer.IsAlive)
                    {
                        _tProducer.Interrupt();
                        _state = StreamerState.Stopping;
                    }
                    else
                        _state = StreamerState.Stopped;
                }
                else
                    throw new Exception($"Illegal State: {_state}");
        }

        /// <summary>
        /// Throws EndOfStreamException if end is reached.
        /// </summary>
        /// <returns></returns>
        protected abstract T Acquire();

        protected bool TryEnqueue(T value)
        {
            if (_state == StreamerState.Started)
            {
                Enqueue0(value);
                return true;
            }
            return false;
        }

        protected void Enqueue(T sample)
        {
            if (_state != StreamerState.Started)
                throw new Exception($"Illegal State: {_state}");
            Enqueue0(sample);
        }

        private void Enqueue0(T sample)
        {
            lock (_queued) _queued.AddLast(sample);
            _semaphore.Release();
        }

        private void AsyncProducer()
        {
            try
            {
                while (_state == StreamerState.Started)
                {
                    var value = Acquire();
                    if (Accept(value)) 
                        Enqueue0(value);
                }
            }
            catch (ThreadInterruptedException) { }
            catch (EndOfStreamException) { }
            finally
            {
                lock (_stateLock)
                    if (_state == StreamerState.Stopping)
                        _state = StreamerState.Stopped;
            }
        }

        private void AsyncConsumer()
        {
            try
            {
                for (;;)
                {
                    if (!_semaphore.WaitOne(500))
                        if (_state == StreamerState.Stopped)
                            break;
                        else
                            continue;
                    T value;
                    lock (_queued)
                    {
                        value = _queued.First.Value;
                        _queued.RemoveFirst();
                    }
                    Dispatch(value);
                }
            }
            catch (ThreadInterruptedException) { }
            finally
            {
                lock (_queued) _queued.Clear();
                Stopped?.Invoke(this, EventArgs.Empty);
            }
        }

    }

    public abstract class TimestampedStreamer<T> : AsyncStreamer<Timestamped<T>>
    {

        protected readonly IClock Clock;

        protected TimestampedStreamer([NotNull] string streamId, [NotNull] IClock clock) : base(streamId) =>
            Clock = clock ?? throw new ArgumentNullException(nameof(clock));

        protected Timestamped<T> WithTimestamp(T t) => new Timestamped<T>(Clock.Time, t);

    }

}
