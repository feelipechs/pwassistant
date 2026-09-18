namespace PwAssistant.WinApi;

/// <summary>
/// Explicit user-requested focus changes (capture targeting, B4 window
/// switching). Returns whether the foreground moved. Failures never throw.
/// This is NOT input automation — dispatch stays focus-free (law #1).
/// </summary>
public static class WindowFocus
{
    private const int SW_RESTORE = 9;
    private const byte VK_MENU = 0x12;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static bool BringToFront(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero || !NativeMethods.IsWindow(windowHandle))
            return false;

        if (NativeMethods.SetForegroundWindow(windowHandle))
            return true;

        // A background process is denied the foreground (the taskbar flashes
        // instead). Step 2: restore if minimized, tap Alt so our thread owns
        // recent input (the documented foreground grant), retry. No injection
        // of any kind; the Alt down+up changes no game state.
        try
        {
            NativeMethods.ShowWindow(windowHandle, SW_RESTORE);
            NativeMethods.keybd_event(VK_MENU, 0, 0, UIntPtr.Zero);
            NativeMethods.keybd_event(VK_MENU, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            if (NativeMethods.SetForegroundWindow(windowHandle))
                return true;
        }
        catch (Exception)
        {
            return false;
        }

        // Step 3 (last resort): attach our input to the foreground thread.
        IntPtr foreground = NativeMethods.GetForegroundWindow();
        uint foregroundThread = 0;
        if (foreground != IntPtr.Zero)
            foregroundThread = NativeMethods.GetWindowThreadProcessId(foreground, out _);
        uint ourThread = NativeMethods.GetCurrentThreadId();
        if (foregroundThread == 0 || foregroundThread == ourThread)
            return false;

        try
        {
            if (!NativeMethods.AttachThreadInput(ourThread, foregroundThread, true))
                return false;
            try
            {
                return NativeMethods.SetForegroundWindow(windowHandle);
            }
            finally
            {
                NativeMethods.AttachThreadInput(ourThread, foregroundThread, false);
            }
        }
        catch (Exception)
        {
            // Best effort by contract; never propagate into hook callbacks.
            return false;
        }
    }
}
