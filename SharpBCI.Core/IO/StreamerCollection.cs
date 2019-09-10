using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using MarukoLib.Lang.Concurrent;

namespace SharpBCI.Core.IO
{

    /// <summary>
    /// The StreamerCollection is used to manage multiple streamers.
    /// </summary>
    public class StreamerCollection
    {

        private readonly LinkedList<IStreamer> _streamers = new LinkedList<IStreamer>();

        private readonly AtomicInt _state = new AtomicInt(0);

        public StreamerCollection(params IStreamer[] streamers) => Add(streamers);

        /// <summary>
        /// Add streamers into current StreamerCollection.
        /// </summary>
        /// <param name="streamers">The given streamers to add into current collection.</param>
        public void Add(params IStreamer[] streamers)
        {
            if (streamers.Length == 0) return;
            if (_state.Value != 0) throw new Exception("The StreamerCollection can only be modified while it is un-started.");
            foreach (var streamer in streamers)
                _streamers.AddLast(streamer);
        }

        /// <summary>
        /// Find the first streamer that is based on the specified type.
        /// </summary>
        /// <typeparam name="T">The desired value type.</typeparam>
        /// <returns>First eligible value or default value will be returned.</returns>
        public T FindFirstOrDefault<T>() => FindFirstOrDefault<T>(null);

        /// <summary>
        /// Find the first streamer that is based on the specified type and satisfies a specified condition.
        /// </summary>
        /// <typeparam name="T">The desired value type.</typeparam>
        /// <param name="filter">A function to test each element for a condition.</param>
        /// <returns>First eligible value or default value will be returned.</returns>
        public T FindFirstOrDefault<T>([CanBeNull] Predicate<T> filter) => TryFindFirst(filter, out var streamer) ? streamer : default;

        /// <summary>
        /// Try find the first streamer that is based on the specified type and satisfies a specified condition.
        /// </summary>
        /// <typeparam name="T">The desired value type.</typeparam>
        /// <param name="streamer">First eligible value or default value.</param>
        /// <returns><see langword="true" /> if one of elements was selected as result; otherwise, <see langword="false" /> if  default value was returned.</returns>
        public bool TryFindFirst<T>(out T streamer) => TryFindFirst(null, out streamer);

        /// <summary>
        /// Try find the first streamer that is based on the specified type and satisfies a specified condition.
        /// </summary>
        /// <typeparam name="T">The desired value type.</typeparam>
        /// <param name="filter">A function to test each element for a condition.</param>
        /// <param name="streamer">First eligible value or default value.</param>
        /// <returns><see langword="true" /> if one of elements was selected as result; otherwise, <see langword="false" /> if  default value was returned.</returns>
        public bool TryFindFirst<T>([CanBeNull] Predicate<T> filter, out T streamer)
        {
            var typed = _streamers.OfType<T>();
            foreach (var current in _streamers.OfType<T>())
                if (filter?.Invoke(current) ?? true)
                {
                    streamer = current;
                    return true;
                }
            streamer = default;
            return false;
        }

        /// <summary>
        /// Find all streamer that is based on the specified type.
        /// </summary>
        /// <typeparam name="T">The desired value type.</typeparam>
        /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements that match conditions.</returns>
        public IEnumerable<T> FindAll<T>() => FindAll<T>(null);

        /// <summary>
        /// Find a streamer that is based on the specified type and satisfies a specified condition.
        /// </summary>
        /// <typeparam name="T">The desired value type.</typeparam>
        /// <param name="filter">A function to test each element for a condition.</param>
        /// <returns>An <see cref="T:System.Collections.Generic.IEnumerable`1" /> that contains elements that match conditions.</returns>
        public IEnumerable<T> FindAll<T>([CanBeNull] Predicate<T> filter)
        {
            var typed = _streamers.OfType<T>();
            return filter == null ? typed : typed.Where(v => filter(v));
        }

        /// <summary>
        /// Start all of streamers in this streamer collection.
        /// </summary>
        public void Start()
        {
            if (!_state.SetIf(0, 1)) return;
            foreach (var streamer in _streamers)
                streamer.Start();
        }

        /// <summary>
        /// Stop all of streamers in this streamer collection.
        /// </summary>
        public void Stop()
        {
            if (!_state.SetIf(1, 2)) return;
            foreach (var streamer in _streamers)
                streamer.Stop();
        }

    }

}
