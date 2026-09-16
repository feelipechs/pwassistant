using System.Runtime.InteropServices;

namespace PwHelper.WinApi;

/// <summary>Physical left-click observed on the master window (Sync Click capture).</summary>
public sealed record MasterClick(int ScreenX, int ScreenY, int ProcessId);

/// <summary>
/// Global low-level mouse hook (WH_MOUSE_LL). Only left-button-down events
/// are surfaced; filtering (registered game window, enabled flag) is owned
/// by the caller. Posted replicas never pass through the real input system,
/// so the hook cannot echo them.
/// </summary>
public sealed class MouseHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201;

    private NativeMethods.LowLevelMouseProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _disposed;

    public event Action<MasterClick>? LeftButtonDown;

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;
        _proc = HookCallback;
        _hookId = NativeMethods.SetWindowsHookEx(
            WH_MOUSE_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
        if (_hookId == IntPtr.Zero)
            throw new WinApiException("Failed to install the mouse hook.");
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_LBUTTONDOWN)
        {
            var data = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
            LeftButtonDown?.Invoke(new MasterClick(data.pt.X, data.pt.Y, ProcessId: 0));
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Stop();
        GC.SuppressFinalize(this);
    }
}
