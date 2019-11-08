using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Extensions.IO.Devices.VideoSources;

namespace SharpBCI.Extensions.Data
{

    public interface IRecord
    {

        ulong Index { get; }

        ulong Timestamp { get; }

    }

    public static class RecordExt
    {

        public static ulong Timestamp(this IRecord record) => record.Timestamp;

        public static IEnumerable<T> OrderByTimestamp<T>(this IEnumerable<T> records, bool ascending = true) where T : IRecord =>
            ascending ? records.OrderBy(t => Timestamp(t)) : records.OrderByDescending(t => Timestamp(t));

        [SuppressMessage("ReSharper", "ConvertIfStatementToSwitchStatement")]
        [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
        public static IEnumerable<T> InTimeRange<T>(this IEnumerable<T> records, ulong? startTimestamp, ulong? endTimestamp) where T : IRecord
        {
            if (startTimestamp == null && endTimestamp == null)
                return records;
            if (startTimestamp == null)
                return records.Where(record => record.Timestamp <= endTimestamp.Value);
            if (endTimestamp == null)
                return records.Where(record => record.Timestamp >= startTimestamp.Value);
            if (startTimestamp.Value > endTimestamp.Value)
                return EmptyArray<T>.Instance;
            return records.Where(record => record.Timestamp >= startTimestamp.Value && record.Timestamp <= endTimestamp.Value);
        }

        public static ulong GetDuration<T>(this Pair<T> records) where T : IRecord => records.Left.Timestamp > records.Right.Timestamp
                ? records.Left.Timestamp - records.Right.Timestamp : records.Right.Timestamp - records.Left.Timestamp;

    }

    public class MarkerRecord : IRecord
    {

        public delegate T Initializer<out T>(int marker, ulong index, ulong timestamp);

        public MarkerRecord(int marker, ulong index, ulong timestamp)
        {
            Marker = marker;
            Index = index;
            Timestamp = timestamp;
        }

        public static bool TryParse(string line, ulong index, out MarkerRecord markerRecord) => TryParse(line, index, (marker, idx, timestamp) =>
            new MarkerRecord(marker, idx, timestamp), out markerRecord);

        public static bool TryParse<T>(string line, ulong index, Initializer<T> initializer, out T result)
        {
            result = default;
            var segments = line?.Split(',');
            if (segments == null || segments.Length != 2) return false;
            if (!int.TryParse(segments[0], out var marker) || !ulong.TryParse(segments[1], out var timestamp)) return false;
            result = initializer(marker, index, timestamp);
            return true;
        }

        public int Marker { get; }

        public ulong Index { get; }

        public ulong Timestamp { get; }

    }

    public static class MarkerRecordExt
    {

        public static IEnumerable<Pair<MarkerRecord>> Intervals(this IEnumerable<MarkerRecord> records, int startMarker, int endMarker)
        {
            if (startMarker == endMarker) throw new ArgumentException();
            if (records == null) yield break;
            MarkerRecord start = null;
            foreach (var record in records)
            {
                if (record.Marker != startMarker && record.Marker != endMarker) continue;
                if (record.Marker == startMarker)
                    start = record;
                else if (record.Marker == endMarker && start != null)
                {
                    var interval = new Pair<MarkerRecord>(start, record);
                    start = null;
                    yield return interval;
                }
            }
        }

    }

    public class BiosignalRecord : IRecord
    {

        public delegate T Initializer<out T>(double[] values, ulong index, ulong timestamp);

        public BiosignalRecord(double[] values, ulong index, ulong timestamp)
        {
            Values = values;
            Index = index;
            Timestamp = timestamp;
        }

        public static bool TryParse(string line, ulong index, out BiosignalRecord biosignalRecord) => TryParse(line, index, (values, idx, timestamp) => new BiosignalRecord(values, idx, timestamp), out biosignalRecord);

        public static bool TryParse<T>(string line, ulong index, Initializer<T> initializer, out T result)
        {
            result = default;
            var segments = line?.Split(',');
            if (segments == null || segments.Length < 2) return false;
            if (!ulong.TryParse(segments[segments.Length - 1], out var timestamp)) return false;
            var values = new double[segments.Length - 1];
            for (var i = 0; i < values.Length; i++)
                if (!double.TryParse(segments[i], out var value))
                    return false;
                else
                    values[i] = value;
            result = initializer(values, index, timestamp);
            return true;
        }

        public static IList<BiosignalRecord> SmoothTimestamp(IEnumerable<BiosignalRecord> records, double? recordInterval = null) 
        {
            var list = records.ToList();
            if (list.Count <= 1) return list;
            ulong min = ulong.MaxValue, max = ulong.MinValue;
            foreach (var record in list)
            {
                var t = record.Timestamp;
                if (t < min) min = t;
                if (t > max) max = t;
            }
            var interval = recordInterval ?? (max - min) / (double)list.Count;
            for (var i = 0; i < list.Count; i++)
            {
                var record = list[i];
                var t = min + (ulong) (i * interval);
                list[i] = new BiosignalRecord(record.Values, record.Index, t);
            }
            return list;
        }

        public double[] Values { get; }

        public ulong Index { get; }

        public ulong Timestamp { get; }

    }

    public class GazePointRecord : IRecord
    {

        public GazePointRecord(double x, double y, ulong index, ulong timestamp)
        {
            X = x;
            Y = y;
            Index = index;
            Timestamp = timestamp;
        }

        public static bool TryParse(string line, ulong index, out GazePointRecord result)
        {
            result = default;
            var segments = line?.Split(',', ';');
            if (segments == null || segments.Length != 3) return false;
            if (!ulong.TryParse(segments[2], out var timestamp)) return false;
            if (!double.TryParse(segments[0], out var x)) return false;
            if (!double.TryParse(segments[1], out var y)) return false;
            result = new GazePointRecord(x, y, index, timestamp);
            return true;
        }

        public double X { get; }

        public double Y { get; }

        public ulong Index { get; }

        public ulong Timestamp { get; }

    }

    public class VideoFrameRecord : IRecord
    {

        public VideoFrameRecord(IVideoFrame frame, ulong index, ulong timestamp)
        {
            Frame = frame;
            Index = index;
            Timestamp = timestamp;
        }

        public static ulong ReadTimestampAndSkip(Stream stream)
        {
            var header = VideoFramesFileWriter.ReadHeader(stream);
            stream.SkipBytes(header.DataLength);
            return (ulong)header.Timestamp;
        }

        public static VideoFrameRecord Read(Stream stream, ulong index)
        {
            VideoFramesFileWriter.Read(stream, out var timestamp, out var frame);
            return new VideoFrameRecord(frame, index, (ulong)timestamp);
        }

        public IVideoFrame Frame { get; }

        public ulong Index { get; }

        public ulong Timestamp { get; }

    }

}
