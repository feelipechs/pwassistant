using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using PwAssistant.WinApi;

namespace PwAssistant.App.Views;

/// <summary>
/// Clamps maximized borderless windows (ThemedWindow) to the monitor work
/// area so the taskbar stays visible. Attach once per window; the OS would
/// otherwise stretch WindowStyle=None over the taskbar.
/// </summary>
internal static class MaximizeClamp
{
    private const int WM_GETMINMAXINFO = 0x0024;

    [StructLayout(LayoutKind.Sequential)]
    private struct Point2
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point2 Reserved;
        public Point2 MaxSize;
        public Point2 MaxPosition;
        public Point2 MinTrackSize;
        public Point2 MaxTrackSize;
    }

    public static void Attach(Window window) =>
        window.SourceInitialized += (_, _) =>
        {
            IntPtr hwnd = new WindowInteropHelper(window).Handle;
            HwndSource.FromHwnd(hwnd)?.AddHook(WndProc);
        };

    private static IntPtr WndProc(
        IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg != WM_GETMINMAXINFO)
            return IntPtr.Zero;
        if (!WindowWorkArea.TryGetWorkArea(hwnd, out int x, out int y, out int w, out int h))
            return IntPtr.Zero;
        MinMaxInfo mmi = Marshal.PtrToStructure<MinMaxInfo>(lParam);
        mmi.MaxPosition = new Point2 { X = x, Y = y };
        mmi.MaxSize = new Point2 { X = w, Y = h };
        Marshal.StructureToPtr(mmi, lParam, fDeleteOld: false);
        handled = true;
        return IntPtr.Zero;
    }
}
