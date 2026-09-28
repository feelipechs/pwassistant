using System.Runtime.InteropServices;

namespace PwAssistant.WinApi;

/// <summary>
/// Monitor work area (screen minus taskbar/docked bars) for a window.
/// Used to clamp maximized borderless windows (ThemedWindow), which the
/// OS would otherwise stretch over the taskbar.
/// </summary>
public static class WindowWorkArea
{
    private const uint MONITOR_DEFAULTTONEAREST = 2;

    public static bool TryGetWorkArea(
        IntPtr hwnd, out int x, out int y, out int width, out int height)
    {
        x = 0;
        y = 0;
        width = 0;
        height = 0;
        if (!OperatingSystem.IsWindows())
            return false;
        IntPtr monitor = NativeMethods.MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);
        if (monitor == IntPtr.Zero)
            return false;
        var info = new NativeMethods.MONITORINFO
        {
            cbSize = Marshal.SizeOf<NativeMethods.MONITORINFO>(),
        };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
            return false;
        x = info.rcWork.Left;
        y = info.rcWork.Top;
        width = info.rcWork.Right - info.rcWork.Left;
        height = info.rcWork.Bottom - info.rcWork.Top;
        return width > 0 && height > 0;
    }
}
