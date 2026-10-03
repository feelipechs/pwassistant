using System.Runtime.InteropServices;

namespace PwAssistant.WinApi;

/// <summary>
/// Native caption theming (Windows 10 20H1+): with standard window frames
/// everywhere (custom TitleBar retired), the caption must follow the app
/// theme or a light caption sits on a dark app. Best effort: old Windows
/// ignores the attribute, the app never breaks. Law #2 holds: the only
/// new P/Invoke lives in NativeMethods.
/// </summary>
public static class CaptionTheme
{
    /// <summary>Applies dark (true) or light (false) native caption. Never throws.</summary>
    public static void Apply(IntPtr windowHandle, bool dark)
    {
        if (windowHandle == IntPtr.Zero)
            return;
        try
        {
            int value = dark ? 1 : 0;
            NativeMethods.DwmSetWindowAttribute(
                windowHandle,
                NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
                ref value,
                Marshal.SizeOf<int>());
        }
        catch
        {
            // Pre-20H1 Windows: attribute unknown, caption stays system.
        }
    }
}
