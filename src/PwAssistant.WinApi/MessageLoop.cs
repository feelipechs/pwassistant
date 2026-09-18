namespace PwAssistant.WinApi;

/// <summary>
/// Minimal Win32 message pump. A thread that installs a low-level hook
/// (WH_MOUSE_LL / WH_KEYBOARD_LL) must pump messages, otherwise the hook
/// callback stops firing. All P/Invoke stays inside this project (law #2).
/// </summary>
public static class MessageLoop
{
    private const uint WM_NULL = 0x0000;

    /// <summary>
    /// Pumps messages on the calling thread until cancellation is requested.
    /// Cancellation wakes the blocked GetMessage via a posted WM_NULL.
    /// </summary>
    public static void RunUntilCancelled(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return;

        uint threadId = NativeMethods.GetCurrentThreadId();
        using (cancellationToken.Register(() =>
            NativeMethods.PostThreadMessageW(threadId, WM_NULL, IntPtr.Zero, IntPtr.Zero)))
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                int result = NativeMethods.GetMessageW(
                    out NativeMethods.MSG msg, IntPtr.Zero, 0, 0);
                if (result <= 0)
                    return;
                NativeMethods.TranslateMessage(ref msg);
                NativeMethods.DispatchMessageW(ref msg);
            }
        }
    }
}
