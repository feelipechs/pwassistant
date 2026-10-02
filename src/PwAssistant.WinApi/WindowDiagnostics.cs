using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace PwAssistant.WinApi;

/// <summary>Active/focus windows of a target thread's input queue.</summary>
public sealed record GuiState(IntPtr ActiveWindow, IntPtr FocusWindow);

/// <summary>
/// Read-only window diagnostics for the dispatch A/B (verbose log only).
/// Never throws; unknown reads as zero/empty. No behavior change, no sends
/// except the WM_NULL pump probe (diagnostic round-trip, no game effect).
/// </summary>
public static class WindowDiagnostics
{
    private const uint WM_NULL = 0x0000;
    private const uint SMTO_BLOCK = 0x0001;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    /// <summary>
    /// The target thread's own view: which window IT considers active and
    /// focused. Fake posted ACTIVATE messages never change these; only the
    /// real OS state does — that is exactly the discriminator (H1 vs H2).
    /// </summary>
    public static GuiState GetGuiState(IntPtr windowHandle)
    {
        try
        {
            if (windowHandle == IntPtr.Zero || !NativeMethods.IsWindow(windowHandle))
                return new GuiState(IntPtr.Zero, IntPtr.Zero);
            uint threadId = NativeMethods.GetWindowThreadProcessId(windowHandle, out _);
            if (threadId == 0)
                return new GuiState(IntPtr.Zero, IntPtr.Zero);
            var info = new NativeMethods.GUITHREADINFO { cbSize = Marshal.SizeOf<NativeMethods.GUITHREADINFO>() };
            if (!NativeMethods.GetGUIThreadInfo(threadId, ref info))
                return new GuiState(IntPtr.Zero, IntPtr.Zero);
            return new GuiState(info.hwndActive, info.hwndFocus);
        }
        catch
        {
            return new GuiState(IntPtr.Zero, IntPtr.Zero);
        }
    }

    /// <summary>
    /// Pump round-trip latency in ms (SendMessageTimeout WM_NULL). A hung or
    /// fully starved pump hits the timeout (-1); a live one answers in ~ms.
    /// </summary>
    public static int ProbePumpMs(IntPtr windowHandle, int timeoutMs = 500)
    {
        try
        {
            if (windowHandle == IntPtr.Zero || !NativeMethods.IsWindow(windowHandle))
                return -1;
            Stopwatch sw = Stopwatch.StartNew();
            IntPtr ok = NativeMethods.SendMessageTimeoutW(
                windowHandle, WM_NULL, IntPtr.Zero, IntPtr.Zero,
                SMTO_BLOCK | SMTO_ABORTIFHUNG, (uint)timeoutMs, out _);
            sw.Stop();
            return ok == IntPtr.Zero ? -1 : (int)sw.ElapsedMilliseconds;
        }
        catch
        {
            return -1;
        }
    }

    /// <summary>
    /// Every visible top-level window of a PID with class + title. Kills the
    /// "wrong window with ok=1" hypothesis: matches[0] is what we send to.
    /// </summary>
    public static IReadOnlyList<string> DescribeWindows(int processId)
    {
        var lines = new List<string>();
        try
        {
            NativeMethods.EnumWindows((hwnd, _) =>
            {
                try
                {
                    NativeMethods.GetWindowThreadProcessId(hwnd, out uint windowPid);
                    if (windowPid != (uint)processId || !NativeMethods.IsWindowVisible(hwnd))
                        return true;
                    var cls = new StringBuilder(256);
                    var title = new StringBuilder(256);
                    NativeMethods.GetClassNameW(hwnd, cls, cls.Capacity);
                    NativeMethods.GetWindowTextW(hwnd, title, title.Capacity);
                    lines.Add($"hwnd=0x{hwnd:X} class='{cls}' title='{Truncate(title.ToString())}'");
                }
                catch
                {
                    // One unreadable window never breaks the listing.
                }
                return true;
            }, IntPtr.Zero);
        }
        catch
        {
            // Best effort by contract.
        }
        return lines;
    }

    /// <summary>
    /// Effective ForegroundLockTimeout in ms (runtime value, NOT the
    /// registry). Volatile by design: any process may rewrite it via
    /// SPI_SETFOREGROUNDLOCKTIMEOUT without persisting, so the registry
    /// (stock 200000) proves nothing about this session. uint.MaxValue
    /// when unreadable (fail-closed: treated as maximum protection).
    /// </summary>
    public static uint GetForegroundLockTimeoutMs()
    {
        try
        {
            uint timeout = 0;
            if (NativeMethods.SystemParametersInfo(0x2000, 0, ref timeout, 0))
                return timeout;
            return uint.MaxValue;
        }
        catch
        {
            return uint.MaxValue;
        }
    }

    /// <summary>
    /// True when the lock is effectively disabled (any process may steal
    /// the foreground): every SetForegroundWindow is granted, every
    /// dispatch visibly switches. Seen in the wild (~1 ms with a stock
    /// 200000 registry); cause unknown, suspected launcher/helper
    /// startup write. Threshold 5 s keeps a wide margin from stock.
    /// </summary>
    public static bool IsForegroundLockDisabled(uint timeoutMs) => timeoutMs < 5000;

    private static string Truncate(string value, int max = 48)
    {
        string flat = value.Replace('\r', ' ').Replace('\n', ' ');
        return flat.Length <= max ? flat : flat[..max] + "…";
    }
}
