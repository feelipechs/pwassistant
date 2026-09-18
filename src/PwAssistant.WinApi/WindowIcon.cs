namespace PwAssistant.WinApi;

/// <summary>
/// Per-window taskbar icon (class identity on top of the per-account
/// AppUserModelID separation). Loads the class .ico shipped with the app
/// and posts it to the game window. Best effort; failures never throw.
/// Icon handles are session-wide, so posting across processes is sound.
/// </summary>
public static class WindowIcon
{
    private const uint WM_SETICON = 0x0080;
    private const uint IMAGE_ICON = 1;
    private const uint LR_LOADFROMFILE = 0x10;
    private static readonly IntPtr ICON_SMALL = IntPtr.Zero;
    private static readonly IntPtr ICON_BIG = (IntPtr)1;

    public static bool TrySetIcon(IntPtr windowHandle, string iconPath)
    {
        if (windowHandle == IntPtr.Zero || string.IsNullOrWhiteSpace(iconPath))
            return false;
        if (!OperatingSystem.IsWindows() || !File.Exists(iconPath))
            return false;

        try
        {
            IntPtr big = NativeMethods.LoadImageW(IntPtr.Zero, iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
            IntPtr small = NativeMethods.LoadImageW(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
            if (big == IntPtr.Zero || small == IntPtr.Zero)
                return false;
            NativeMethods.SendMessageW(windowHandle, WM_SETICON, ICON_BIG, big);
            NativeMethods.SendMessageW(windowHandle, WM_SETICON, ICON_SMALL, small);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
