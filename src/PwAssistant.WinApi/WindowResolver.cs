using System.Text;
using PwAssistant.Core.Input;

namespace PwAssistant.WinApi;

/// <summary>
/// PID → top-level HWND via EnumWindows. The game has no child windows,
/// so the target is always the top-level itself. Never MainWindowHandle
/// cache, never title-as-key, never blind broadcast.
/// </summary>
public sealed class WindowResolver : IWindowResolver
{
    /// <summary>Game window class: preferred when a PID owns several
    /// visible top-levels (e.g. the embedded IE host of the cash-shop).</summary>
    private const string GameWindowClass = "ElementClient Window";

    public IntPtr ResolveWindow(int processId)
    {
        var matches = new List<IntPtr>();

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            NativeMethods.GetWindowThreadProcessId(hwnd, out uint windowPid);
            if (windowPid == (uint)processId && NativeMethods.IsWindowVisible(hwnd))
                matches.Add(hwnd);
            return true;
        }, IntPtr.Zero);

        if (matches.Count == 0)
            return IntPtr.Zero;
        if (matches.Count == 1)
            return matches[0];

        // Several visible top-levels (game + embedded browser host):
        // prefer the game class instead of blind Z-order position.
        foreach (IntPtr hwnd in matches)
        {
            try
            {
                var cls = new StringBuilder(256);
                if (NativeMethods.GetClassNameW(hwnd, cls, cls.Capacity) > 0
                    && cls.ToString() == GameWindowClass)
                    return hwnd;
            }
            catch
            {
                // One unreadable window never breaks resolution.
            }
        }
        return matches[0];
    }

    public bool IsWindowAlive(IntPtr windowHandle) =>
        windowHandle != IntPtr.Zero && NativeMethods.IsWindow(windowHandle);

    public (int Width, int Height) GetClientSize(IntPtr windowHandle)
    {
        if (!NativeMethods.GetClientRect(windowHandle, out NativeMethods.RECT rect))
            throw new WinApiException("GetClientRect failed.");
        return (rect.Right - rect.Left, rect.Bottom - rect.Top);
    }

    public (int X, int Y) ScreenToClientPoint(IntPtr windowHandle, int screenX, int screenY)
    {
        var point = new NativeMethods.POINT { X = screenX, Y = screenY };
        if (!NativeMethods.ScreenToClient(windowHandle, ref point))
            throw new WinApiException("ScreenToClient failed.");
        return (point.X, point.Y);
    }

    public IntPtr ResolveTopWindowAtPoint(int screenX, int screenY) =>
        NativeMethods.WindowFromPoint(new NativeMethods.POINT { X = screenX, Y = screenY });

    public IntPtr GetForegroundWindow() => NativeMethods.GetForegroundWindow();
}

public sealed class WinApiException : Exception
{
    public WinApiException(string message) : base(message) { }
}
