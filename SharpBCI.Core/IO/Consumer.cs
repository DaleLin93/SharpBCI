using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;
using MarukoLib.Lang;
using MarukoLib.Logging;

namespace SharpBCI.Core.IO
{

    /// <summary>
    /// Base interface of consumer.
    /// </summary>
    public interface IConsumer : IPriorityComponent
    {

        void Accept(object value);

    }

    /// <summary>
    /// The consumer interface with generic type.
    /// </summary>
    public interface IConsumer<in T> : IConsumer
    {

        void Accept(T value);

    }

    public abstract class Consumer<T> : IConsumer<T>
    {

        public Type AcceptType => typeof(T);

        public virtual Priority Priority { get; } = Priority.Normal;

        public abstract void Accept(T value);

        void IConsumer.Accept(object value) => Accept((T)value);

    }

    public abstract class TransformedConsumer<TIn, TOut> : IConsumer<TIn>
    {

        private readonly Func<TIn, TOut> _transformer;

        protected TransformedConsumer(Func<TIn, TOut> transformer) => 
            _transformer = transformer ?? throw new ArgumentNullException(nameof(transformer));

        public Type AcceptType => typeof(TIn);

        public virtual Priority Priority { get; } = Priority.Normal;

        public abstract void Accept(TOut value);

        public void Accept(TIn value) => Accept(_transformer(value));

        void IConsumer.Accept(object value) => Accept((TIn)value);

    }

    public sealed class DelegatedConsumer<T> : Consumer<T>
    {

        [NotNull] private readonly Action<T> _delegate;

        public DelegatedConsumer([NotNull] Action<T> @delegate, Priority priority = IO.Priority.Normal)
        {
            _delegate = @delegate ?? throw new ArgumentNullException(nameof(@delegate));
            Priority = priority;
        }

        public override Priority Priority { get; }

        public override void Accept(T value) => _delegate(value);

        public void Accept(object value) => _delegate((T) value);

    }

    public class CachedConsumer<T> : Consumer<T>
    {

        public CachedConsumer(Priority priority = IO.Priority.Lowest) => Priority = priority;

        public T Value { get; set; }

        public override Priority Priority { get; }

        public override void Accept(T value) => Value = value;

    } 

    public class RecordingConsumer<T> : Consumer<T>
    {

        private readonly LinkedList<T> _list = new LinkedList<T>();

        public RecordingConsumer(uint capacity, Priority priority = IO.Priority.Lowest)
        {
            if (capacity == 0) throw new ArgumentException("'capacity' must be positive");
            Capacity = capacity;
            Priority = priority;
        }

        public uint Capacity { get; }

        public override Priority Priority { get; }

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

    public sealed class LogWriter<T> : Consumer<T>
    {

        private static readonly Logger Logger = Logger.GetLogger(typeof(LogWriter<T>));

        [NotNull]
        private readonly Func<T, string> _serializer;

        public LogWriter() : this(value => value?.ToString()) { }

        public LogWriter([NotNull] Func<T, string> serializer) => _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

        public override Priority Priority => Priority.Lowest;

        public override void Accept(T data) => Logger.Info("Accept", "data", _serializer(data));

    }

    public abstract class FileWriter<T> : Consumer<T>, IDisposable
    {

        private readonly object _streamLock = new object();

        private readonly Stream _stream;

        protected FileWriter([NotNull] string fileName, int bufferSize) => _stream = new BufferedStream(File.OpenWrite(fileName), bufferSize);

        public override Priority Priority => Priority.Lowest;

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

        public static DelegatedConsumer<TIn> Map<TIn, TOut>(this IConsumer<TOut> consumer, Func<TIn, TOut> mapFunc) => 
            new DelegatedConsumer<TIn>(input => consumer.Accept(mapFunc(input)), consumer.Priority);

    }

}
