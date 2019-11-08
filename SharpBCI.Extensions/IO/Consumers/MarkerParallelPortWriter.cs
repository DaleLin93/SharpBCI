using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using MarukoLib.Interop;
using MarukoLib.Lang;
using MarukoLib.Lang.Concurrent;
using SharpBCI.Core.Experiment;
using SharpBCI.Core.IO;
using SharpBCI.Extensions.IO.Devices.MarkerSources;

namespace SharpBCI.Extensions.IO.Consumers
{

    [SuppressMessage("ReSharper", "InconsistentNaming")]
    public enum LogicalParallelPort : byte
    {
        LPT1 = 1,
        LPT2 = 2,
        LPT3 = 3,
        LPT4 = 4
    }

    [StreamConsumer(ConsumerName, typeof(Factory), "1.0")]
    public class MarkerParallelPortWriter : StreamConsumer<Timestamped<IMarker>>, IDisposable
    {

        public const string ConsumerName = "Marker Parallel Port Writer";

        public class Factory : StreamConsumerFactory<Timestamped<IMarker>>
        {

            public static readonly Parameter<LogicalParallelPort> ParallelPortAddressParam = Parameter<LogicalParallelPort>.OfEnum("LPT", LogicalParallelPort.LPT1);

            public Factory() : base(ParallelPortAddressParam) { }

            public override IStreamConsumer<Timestamped<IMarker>> Create(Session session, IReadonlyContext context, byte? num)
                => new MarkerParallelPortWriter(ParallelPortAddressParam.Get(context));

        }

        private readonly AtomicPtr _fileHandle;

        private IntPtr _oneByteBuffer;

        public MarkerParallelPortWriter(LogicalParallelPort lpt)
        {
            Port = lpt;
            _fileHandle = new AtomicPtr(Kernel32.CreateFile($"lpt{(byte)lpt}", FileAccess.ReadWrite, FileShare.None, IntPtr.Zero, FileMode.Open, 0, IntPtr.Zero));
            _oneByteBuffer = Marshal.AllocHGlobal(1);
        }

        public LogicalParallelPort Port { get; }

        public bool SendEvent(byte b)
        {
            var fd = _fileHandle.Value;
            if (fd == IntPtr.Zero) return false;
            return Kernel32.WriteFile(fd, _oneByteBuffer, 1, out var written, IntPtr.Zero) && written > 0;
        }

        public override void Accept(Timestamped<IMarker> value) => SendEvent((byte)(value.Value.Code & 0xFF));

        public void Dispose()
        {
            var fd = _fileHandle.Value;
            if (fd == IntPtr.Zero || !_fileHandle.CompareAndSet(fd, IntPtr.Zero)) return;
            Kernel32.CloseHandle(fd);
            Marshal.FreeHGlobal(_oneByteBuffer);
            _oneByteBuffer = IntPtr.Zero;
        }

    }
}
