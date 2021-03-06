﻿using System;
using System.IO;
using System.IO.Compression;
using JetBrains.Annotations;
using MarukoLib.IO;
using MarukoLib.Lang;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices.VideoSources;

namespace SharpBCI.Extensions.IO.Consumers.VideoFrame
{
    [Consumer(ConsumerName, typeof(Factory), "1.0")]
    public class VideoFramesFileWriter : TimestampedFileWriter<IVideoFrame>
    {

        public sealed class Factory : ConsumerFactory<Timestamped<IVideoFrame>>
        {

            public override IConsumer<Timestamped<IVideoFrame>> Create(Session session, IReadonlyContext context, byte? num) =>
                new VideoFramesFileWriter(session.GetDataFileName(FileSuffix), session.CreateTimestamp);

        }

        public struct FrameHeader
        {

            public long Timestamp;

            public uint DataLength;

        }

        public const string FileSuffix = ".vfs";

        public const string ConsumerName = "Video Frames File Writer (*" + FileSuffix + ")";

        private readonly byte[] _longBuffer = new byte[sizeof(long)];

        private readonly bool _compress;

        private readonly ByteArrayStream _byteArrayStream;

        public VideoFramesFileWriter([NotNull] string fileName, long baseTime = 0, int bufferSize = 2048) : this(fileName, false, baseTime, bufferSize) { }

        public VideoFramesFileWriter([NotNull] string fileName, bool compress, long baseTime = 0, int bufferSize = 2048) : base(fileName, bufferSize, baseTime)
        {
            _compress = compress;
            _byteArrayStream = compress ? new ByteArrayStream(1 << 20) : null;
        }

        public static FrameHeader ReadHeader(Stream stream)
        {
            var buffer = new byte[sizeof(long)];
            stream.ReadFully(buffer);
            var timestamp = buffer.ReadInt64FromNetworkOrder();
            stream.ReadFully(buffer, 0, sizeof(uint));
            var length = buffer.ReadUInt32FromNetworkOrder();
            return new FrameHeader{DataLength = length, Timestamp = timestamp};
        }

        public static void Read(Stream stream, out long timestamp, out IVideoFrame frame) =>
            Read(stream, s0 => Devices.VideoSources.VideoFrame.Read(s0), out timestamp, out frame);

        public static void Read(Stream stream, Func<Stream, IVideoFrame> decoder, out long timestamp, out IVideoFrame frame)
        {
            var header = ReadHeader(stream);
            timestamp = header.Timestamp;
            var bytes = new byte[header.DataLength];
            stream.ReadFully(bytes);
            using (var memoryStream = new MemoryStream(bytes))
            using (var deflateStream = new DeflateStream(memoryStream, CompressionMode.Decompress))
                frame = decoder(deflateStream);
        }

        protected override void Write(Stream stream, Timestamped<IVideoFrame> data)
        {
            var t = data.Timestamp - BaseTime;
            lock (_longBuffer)
            {
                _longBuffer.WriteInt64AsNetworkOrder(t);
                stream.Write(_longBuffer, 0, sizeof(long));
            }
            WriteFrame(stream, data.Value);
            stream.Flush();
        }

        private void WriteFrame(Stream stream, IVideoFrame frame)
        {
            lock (_byteArrayStream)
            {
                _byteArrayStream.Position = 0;
                if (_compress)
                {
                    using (var deflateStream = new DeflateStream(_byteArrayStream, CompressionMode.Compress))
                        frame.Write(deflateStream);
                }
                else
                {
                    frame.Write(_byteArrayStream);
                    _byteArrayStream.Close();
                }
                lock (_longBuffer)
                {
                    _longBuffer.WriteUInt32AsNetworkOrder((uint) _byteArrayStream.Position);
                    stream.Write(_longBuffer, 0, sizeof(uint));
                }
                stream.Write(_byteArrayStream.Buffer, 0, (int)_byteArrayStream.Position);
                _byteArrayStream.Reopen();
            }
        }

    }
}