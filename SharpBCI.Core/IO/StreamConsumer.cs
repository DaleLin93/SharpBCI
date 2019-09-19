using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Logging;

namespace SharpBCI.Core.IO
{

    public enum StreamConsumerPriority : byte
    {
        Highest = 0,
        High = 1,
        Normal = 2,
        Low = 3,
        Lowest = 4
    }

    /// <summary>
    /// Base interface of consumer.
    /// </summary>
    public interface IStreamConsumer
    {

        Type AcceptType { get; }

        StreamConsumerPriority Priority { get; }

        void Accept(object value);

    }

    /// <summary>
    /// The consumer interface with generic type.
    /// </summary>
    public interface IStreamConsumer<in T> : IStreamConsumer
    {

        void Accept(T value);

    }

    public abstract class StreamConsumer<T> : IStreamConsumer<T>
    {

        public Type AcceptType => typeof(T);

        public virtual StreamConsumerPriority Priority { get; } = StreamConsumerPriority.Normal;

        public abstract void Accept(T value);

        void IStreamConsumer.Accept(object value) => Accept((T)value);

    }

    public abstract class TransformedStreamConsumer<TIn, TOut> : IStreamConsumer<TIn>
    {

        private readonly Func<TIn, TOut> _transformer;

        protected TransformedStreamConsumer(Func<TIn, TOut> transformer) => 
            _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));

        public Type AcceptType => typeof(TIn);

        public virtual StreamConsumerPriority Priority { get; } = StreamConsumerPriority.Normal;

        public abstract void Accept(TOut value);

        public void Accept(TIn value) => Accept(_transformer(value));

        void IStreamConsumer.Accept(object value) => Accept((TIn)value);

    }

    public sealed class DelegatedStreamConsumer<T> : StreamConsumer<T>
    {

        [NotNull] private readonly Action<T> _delegate;

        public DelegatedStreamConsumer([NotNull] Action<T> @delegate, StreamConsumerPriority priority = StreamConsumerPriority.Normal)
        {
            _delegate = @delegate ?? throw new ArgumentNullException(nameof(@delegate));
            Priority = priority;
        }

        public override StreamConsumerPriority Priority { get; }

        public override void Accept(T value) => _delegate(value);

        public void Accept(object value) => _delegate((T) value);

    }

    public class CachedStreamConsumer<T> : StreamConsumer<T>
    {

        public CachedStreamConsumer(StreamConsumerPriority priority = StreamConsumerPriority.Lowest) => Priority = priority;

        public T Value { get; set; }

        public override StreamConsumerPriority Priority { get; }

        public override void Accept(T value) => Value = value;

    } 

    public class RecordingStreamConsumer<T> : StreamConsumer<T>
    {

        private readonly LinkedList<T> _list = new LinkedList<T>();

        public RecordingStreamConsumer(uint capacity, StreamConsumerPriority priority = StreamConsumerPriority.Lowest)
        {
            if (capacity == 0) throw new ArgumentException("'capacity' must be positive");
            Capacity = capacity;
            Priority = priority;
        }

        public uint Capacity { get; }

        public override StreamConsumerPriority Priority { get; }

        public T[] Values
        {
            get
            {
                lock (_list)
                    return _list.ToArray();
            }
        }

        public bool TryGetLastValue(out T result)
        {
            lock (_list)
                if (_list.Any())
                {
                    result = _list.Last.Value;
                    return true;
                }
            result = default;
            return false;
        }

        public override void Accept(T value)
        {
            lock (_list)
            {
                _list.AddLast(value);
                if (_list.Count > Capacity) _list.RemoveFirst();
            }
        }

        public void Reset()
        {
            lock (_list)
                _list.Clear();
        }

    }

    public sealed class LogWriter<T> : StreamConsumer<T>
    {

        private static readonly Logger Logger = Logger.GetLogger(typeof(LogWriter<T>));

        [NotNull]
        private readonly Func<T, string> _serializer;

        public LogWriter() : this(value => value?.ToString()) { }

        public LogWriter([NotNull] Func<T, string> serializer) => _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

        public override StreamConsumerPriority Priority => StreamConsumerPriority.Lowest;

        public override void Accept(T data) => Logger.Info("Accept", "data", _serializer(data));

    }

    public abstract class FileWriter<T> : StreamConsumer<T>, IDisposable
    {

        private readonly object _streamLock = new object();

        private readonly Stream _stream;

        protected FileWriter([NotNull] string fileName, int bufferSize) => _stream = new BufferedStream(File.OpenWrite(fileName), bufferSize);

        public override StreamConsumerPriority Priority => StreamConsumerPriority.Lowest;

        public override void Accept(T data)
        {
            lock (_streamLock)
                Write(_stream, data);
        }

        public virtual void Dispose()
        {
            lock (_streamLock)
            {
                _stream.Flush();
                _stream.Close();
            }
        }

        protected abstract void Write(Stream stream, T data);

    }

    public abstract class TimestampedFileWriter<T> : FileWriter<Timestamped<T>>
    {

        protected TimestampedFileWriter([NotNull] string fileName, int bufferSize, long baseTime = 0) : base(fileName, bufferSize) => BaseTime = baseTime;

        public long BaseTime { get; }

    }

    public static class ConsumerExt
    {

        public static DelegatedStreamConsumer<TIn> Map<TIn, TOut>(this IStreamConsumer<TOut> consumer, Func<TIn, TOut> mapFunc) => 
            new DelegatedStreamConsumer<TIn>(input => consumer.Accept(mapFunc(input)), consumer.Priority);

    }

}
