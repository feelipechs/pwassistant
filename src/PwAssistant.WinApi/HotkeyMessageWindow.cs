using System.Runtime.InteropServices;

namespace PwAssistant.WinApi;

/// <summary>
/// Message-only window owning a dedicated pump thread. Replaces the WPF
/// HwndSource hook: global hotkeys (RegisterHotKey → WM_HOTKEY) need a
/// handle with a message loop, and Avalonia exposes no WndProc hook — so
/// this sink owns both, with zero UI dependency. Law #2 holds: every
/// new P/Invoke lives in NativeMethods.
/// </summary>
public sealed class HotkeyMessageWindow : IDisposable
{
    private static readonly IntPtr HwndMessage = new(-3);
    private const uint WM_CLOSE = 0x0010;

    private readonly NativeMethods.WindowProc _proc;
    private readonly Thread _pump;
    private IntPtr _handle;
    private uint _pumpThreadId;
    private bool _disposed;

    public event Action<uint, IntPtr, IntPtr>? MessageReceived;

    public IntPtr Handle => _handle;

    public HotkeyMessageWindow()
    {
        _proc = WndProc;
        var cls = new NativeMethods.WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf<NativeMethods.WNDCLASSEX>(),
            lpfnWndProc = _proc,
            hInstance = NativeMethods.GetModuleHandle(null),
            lpszClassName = "PwAssistant.HotkeySink." + Guid.NewGuid().ToString("N"),
        };
        if (NativeMethods.RegisterClassExW(ref cls) == 0)
            throw new WinApiException("RegisterClassEx failed.");

        _handle = NativeMethods.CreateWindowExW(
            0, cls.lpszClassName, string.Empty, 0, 0, 0, 0, 0,
            HwndMessage, IntPtr.Zero, cls.hInstance, IntPtr.Zero);
        if (_handle == IntPtr.Zero)
            throw new WinApiException("CreateWindowEx (message-only) failed.");

        _pump = new Thread(Pump) { IsBackground = true, Name = "HotkeyMessagePump" };
        _pump.Start();

        string className = cls.lpszClassName;
        IntPtr instance = cls.hInstance;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => NativeMethods.UnregisterClassW(className, instance);
    }

    private void Pump()
    {
        _pumpThreadId = NativeMethods.GetCurrentThreadId();
        while (NativeMethods.GetMessageW(out NativeMethods.MSG msg, IntPtr.Zero, 0, 0) > 0)
        {
            NativeMethods.TranslateMessage(ref msg);
            NativeMethods.DispatchMessageW(ref msg);
        }
    }

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        // Message-only windows only get WM_CLOSE when posted explicitly
        // (Dispose below): tear the window down and stop the pump.
        if (msg == WM_CLOSE)
        {
            NativeMethods.DestroyWindow(hWnd);
            NativeMethods.PostQuitMessage(0);
            return IntPtr.Zero;
        }

        try
        {
            MessageReceived?.Invoke(msg, wParam, lParam);
        }
        catch
        {
            // A throwing subscriber must never kill the pump thread.
        }
        return NativeMethods.DefWindowProcW(hWnd, msg, wParam, lParam);
    }

    private const uint WM_QUIT = 0x0012;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        MessageReceived = null;
        IntPtr handle = Interlocked.Exchange(ref _handle, IntPtr.Zero);
        if (handle != IntPtr.Zero)
        {
            // Wake the pump two ways: WM_CLOSE tears the window down on the
            // healthy path; WM_QUIT straight to the pump thread unblocks
            // GetMessage even when dispatch is stuck. Teardown never waits
            // long: the 2 s tray-exit stall was this Join expiring.
            NativeMethods.PostMessageW(handle, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);
            uint threadId = _pumpThreadId;
            if (threadId != 0)
                NativeMethods.PostThreadMessageW(threadId, WM_QUIT, IntPtr.Zero, IntPtr.Zero);
            _pump.Join(TimeSpan.FromMilliseconds(500));
        }
        GC.SuppressFinalize(this);
    }
}
