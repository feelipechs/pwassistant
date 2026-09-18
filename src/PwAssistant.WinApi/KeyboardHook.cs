using System.Runtime.InteropServices;

namespace PwAssistant.WinApi;

/// <summary>Low-level key transition observed by <see cref="KeyboardHook"/>.</summary>
public sealed record KeyTransition(int VirtualKey, bool IsKeyDown);

/// <summary>
/// Global low-level keyboard hook (WH_KEYBOARD_LL). Surfaces key down/up
/// transitions; filtering and tap-vs-hold semantics are owned by the caller.
/// Same pattern as <see cref="MouseHook"/> (message pump required).
/// </summary>
public sealed class KeyboardHook : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    private NativeMethods.LowLevelKeyboardProc? _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private bool _disposed;

    public event Action<KeyTransition>? KeyTransition;

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;
        _proc = HookCallback;
        _hookId = NativeMethods.SetWindowsHookEx(
            WH_KEYBOARD_LL, _proc, NativeMethods.GetModuleHandle(null), 0);
        if (_hookId == IntPtr.Zero)
            throw new WinApiException("Failed to install the keyboard hook.");
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
        if (nCode >= 0)
        {
            int msg = wParam.ToInt32();
            if (msg is WM_KEYDOWN or WM_SYSKEYDOWN or WM_KEYUP or WM_SYSKEYUP)
            {
                var data = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                KeyTransition?.Invoke(new KeyTransition(
                    (int)data.vkCode, msg is WM_KEYDOWN or WM_SYSKEYDOWN));
            }
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
