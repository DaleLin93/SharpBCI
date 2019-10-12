using JetBrains.Annotations;
using MarukoLib.Lang;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;

namespace SharpBCI.Core.IO
{

    public enum StreamerState
    {
        Initialized = 0, Started = 1, Stopping = 2, Stopped = 3
    }

    public interface IStreamer
    {

        string StreamId { get; }

        Type ValueType { get; }

        int AttachedConsumerCount { get; }

        StreamerState State { get; }

        void Start();

        void Stop();

        void Attach(IStreamConsumer consumer);

        bool Detach(IStreamConsumer consumer);

        IEnumerable<T> FindConsumers<T>() where T : IStreamConsumer;

    }

    public abstract class Streamer : IStreamer
    {

        private readonly LinkedList<IStreamConsumer> _consumers = new LinkedList<IStreamConsumer>();

        private readonly ReaderWriterLockSlim _consumersLock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);

        protected Streamer([NotNull] string streamId, [NotNull] Type valueType)
        {
            StreamId = streamId ?? throw new ArgumentNullException(nameof(streamId));
            ValueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        }

        public string StreamId { get; }

        public Type ValueType { get; }

        public int AttachedConsumerCount => _consumers.Count;

        public abstract StreamerState State { get; }

        public abstract void Start();

        public abstract void Stop();

        public void Attach(IStreamConsumer consumer)
        {
            if (!consumer.AcceptType.IsAssignableFrom(ValueType)) 
                throw new ArgumentException($"Type not match, streamer value type: {ValueType}, consumer accept type: {consumer.AcceptType}");
            var priority = consumer.Priority;
            try
            {
                _consumersLock.EnterWriteLock();
                if (!_consumers.IsEmpty())
                {
                    if (_consumers.Contains(consumer)) throw new ArgumentException("The given consumer is already attached");
                    var node = _consumers.First;
                    while (node != null)
                    {
                        if (node.Value.Priority > priority)
                        {
                            _consumers.AddBefore(node, consumer);
                            return;
                        }
                        node = node.Next;
                    }
                }
                _consumers.AddLast(consumer);
            }
            finally
            {
                _consumersLock.ExitWriteLock();
            }
        }

        public bool Detach(IStreamConsumer consumer)
        {
            if (!consumer.AcceptType.IsAssignableFrom(ValueType)) return false;
            try
            {
                _consumersLock.EnterWriteLock();
                return _consumers.Remove(consumer);
            }
            finally
            {
                _consumersLock.ExitWriteLock();
            }
        }

        public IEnumerable<TC> FindConsumers<TC>() where TC : IStreamConsumer
        {
            var consumers = new LinkedList<TC>();
            var type = typeof(TC);
            try
            {
                _consumersLock.EnterReadLock();
                foreach (var consumer in _consumers.Where(consumer => type.IsInstanceOfType(consumer)))
                    consumers.AddLast((TC)consumer);
            }
            finally
            {
                _consumersLock.ExitReadLock();
            }
            return consumers;
        }

        protected void Consume(object value)
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
                    Enqueue0(Acquire());
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
                    Consume(value);
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
