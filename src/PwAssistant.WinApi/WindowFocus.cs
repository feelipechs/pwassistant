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
    private const int GWL_EXSTYLE = -20;
    private static readonly IntPtr WS_EX_NOACTIVATE = (IntPtr)0x08000000;

    /// <summary>
    /// Makes a floating window never steal activation: mouse clicks reach
    /// its controls while the previous foreground window keeps the keyboard.
    /// </summary>
    public static void PreventActivation(IntPtr windowHandle)
    {
        IntPtr style = NativeMethods.GetWindowLongPtr(windowHandle, GWL_EXSTYLE);
        NativeMethods.SetWindowLongPtr(windowHandle, GWL_EXSTYLE, (IntPtr)(style.ToInt64() | WS_EX_NOACTIVATE.ToInt64()));
    }

    /// <summary>
    /// Best-effort live window-state probes for fire diagnostics.
    /// Never throw; a dead handle simply reports false.
    /// </summary>
    public static bool IsAlive(IntPtr windowHandle)
    {
        try
        {
            return windowHandle != IntPtr.Zero && NativeMethods.IsWindow(windowHandle);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsVisible(IntPtr windowHandle)
    {
        try
        {
            return windowHandle != IntPtr.Zero && NativeMethods.IsWindowVisible(windowHandle);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsMinimized(IntPtr windowHandle)
    {
        try
        {
            return windowHandle != IntPtr.Zero && NativeMethods.IsIconic(windowHandle);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Single real SetForegroundWindow call, no forcing of any kind: no
    /// Alt-tap, no thread attach, no restore. Result depends on the
    /// foreground grant: denied (taskbar flashes) when our process holds
    /// no recent input — the Helper's background case; granted (visible
    /// switch) when it does (e.g. right after a click on our own window).
    /// Never throws; the caller decides from the raw result.
    /// </summary>
    public static bool TrySetSoft(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero || !NativeMethods.IsWindow(windowHandle))
            return false;

        try
        {
            return NativeMethods.SetForegroundWindow(windowHandle);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Best-effort owner process name of a window (grant diagnostics).
    /// Never throws; null when unknown.
    /// </summary>
    public static string? GetOwnerProcessName(IntPtr windowHandle)
    {
        try
        {
            if (windowHandle == IntPtr.Zero || !NativeMethods.IsWindow(windowHandle))
                return null;
            NativeMethods.GetWindowThreadProcessId(windowHandle, out uint pid);
            using System.Diagnostics.Process proc = System.Diagnostics.Process.GetProcessById((int)pid);
            return proc.ProcessName;
        }
        catch
        {
            return null;
        }
    }

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
