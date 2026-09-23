namespace PwAssistant.WinApi;

/// <summary>
/// Manual window resizing (SC_SIZE) for transparent windows, where
/// WindowChrome's invisible resize border doesn't engage. No injection:
/// the OS runs the modal size loop from the user's own mouse gesture.
/// </summary>
public static class WindowResize
{
    private const uint WM_SYSCOMMAND = 0x0112;
    private const int SC_SIZE = 0xF000;
    private const int WMSZ_BOTTOMRIGHT = 8;

    public static void BeginBottomRight(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero || !NativeMethods.IsWindow(windowHandle))
            return;
        try
        {
            NativeMethods.ReleaseCapture();
            NativeMethods.SendMessageW(
                windowHandle, WM_SYSCOMMAND, (IntPtr)(SC_SIZE + WMSZ_BOTTOMRIGHT), IntPtr.Zero);
        }
        catch (Exception)
        {
            // Best effort by contract; the window simply doesn't resize.
        }
    }
}
