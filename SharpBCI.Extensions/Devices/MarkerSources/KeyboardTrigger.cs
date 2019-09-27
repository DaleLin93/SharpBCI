using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Input;
using MarukoLib.Interop;
using MarukoLib.Lang;
using MarukoLib.Lang.Exceptions;

namespace SharpBCI.Extensions.Devices.MarkerSources
{

    [Device(DeviceName, typeof(Factory), "1.0")]
    public sealed class KeyboardTrigger : MarkerSource
    {

        public const string DeviceName = "Keyboard Trigger";

        public enum KeyAction
        {
            KeyDown, KeyUp
        }

        public class Factory : DeviceFactory<KeyboardTrigger, IMarkerSource>
        {

            public static readonly Parameter<KeyAction> OnKeyActionParam = Parameter<KeyAction>.OfEnum("On Key Action", KeyAction.KeyUp);

            public Factory() : base(OnKeyActionParam) { }

            public override KeyboardTrigger Create(IReadonlyContext context) => new KeyboardTrigger(OnKeyActionParam.Get(context));

        }

        private readonly KeyAction _onKeyAction;

        private readonly LinkedList<IMarker> _marks = new LinkedList<IMarker>();

        private readonly Semaphore _semaphore = new Semaphore(0, int.MaxValue);

        private readonly IntPtr _hModule;

        private readonly User32.HookProc _hookProc;

        private IntPtr _hookHandle;

        private bool _started = false;

        private KeyboardTrigger(KeyAction onKeyAction)
        {
            _onKeyAction = onKeyAction;
            var module = Process.GetCurrentProcess().MainModule ?? throw new StateException("MainModule of process is null");
            _hModule = Kernel32.GetModuleHandle(module.ModuleName);
            _hookProc = KeyboardHookProc;
        }

        public override void Open()
        {
            _started = true;
            _hookHandle = User32.SetWindowsHookEx(13, _hookProc, _hModule, 0);
        }

        public override IMarker Read()
        {
            while (!_semaphore.WaitOne(100))
                if (!_started) return null;
            lock (_marks)
            {
                var mark = _marks.First.Value;
                _marks.RemoveFirst();
                return mark;
            }
        }

        public override void Shutdown()
        {
            User32.UnhookWindowsHookEx(_hookHandle);
            _started = false;
            lock (_marks)
            {
                while (_marks.Count > 0)
                {
                    _marks.RemoveLast();
                    _semaphore.WaitOne(0);
                }
            }
        }

        public override void Dispose() { }

        private IntPtr KeyboardHookProc(int nCode, int wParam, int lParam)
        {
            if (nCode >= 0)
                switch (_onKeyAction)
                {
                    case KeyAction.KeyDown when (wParam == User32.WM_KEYDOWN || wParam == User32.WM_SYSKEYDOWN):
                    case KeyAction.KeyUp when (wParam == User32.WM_KEYUP || wParam == User32.WM_SYSKEYUP):
                        Enqueue(((User32.KeyboardHookStruct)Marshal.PtrToStructure((IntPtr)lParam, typeof(User32.KeyboardHookStruct))).vkCode);
                        break;
                }
            return User32.CallNextHookEx(_hookHandle, nCode, wParam, lParam);

        }

        private void Enqueue(int vkCode)
        {
            var mark = new Marker("Key." + KeyInterop.KeyFromVirtualKey(vkCode), vkCode);
            lock (_marks) _marks.AddLast(mark);
            _semaphore.Release();
        }

    }

}
