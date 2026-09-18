namespace PwHelper.WinApi;

/// <summary>
/// Explicit user-requested focus changes (capture targeting, B4 window
/// switching). Fire-and-forget: a failure never blocks the caller.
/// This is NOT input automation — dispatch stays focus-free (law #1).
/// </summary>
public static class WindowFocus
{
    public static void BringToFront(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero || !NativeMethods.IsWindow(windowHandle))
            return;
        NativeMethods.SetForegroundWindow(windowHandle);
    }
}
