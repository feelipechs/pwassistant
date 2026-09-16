using PwHelper.Core.Input;

namespace PwHelper.WinApi;

/// <summary>Priming/send timings for the validated recipe. Adjustable for tuning, never zeroed.</summary>
public sealed record WinApiTiming(
    int PrimeStepMs = 30,
    int KeyHoldMs = 50,
    int MousePrimeStepMs = 20,
    int ClickHoldMs = 50);

/// <summary>
/// Default and only v1 input strategy: PostMessage with activation priming.
/// No focus steal, no flicker, no driver, no injection.
/// A non-zero PostMessage return only proves delivery — never game effect.
/// </summary>
public sealed class PostMessageBackgroundStrategy : IInputStrategy
{
    private readonly WinApiTiming _timing;
    private readonly bool _applyHygiene;

    public PostMessageBackgroundStrategy(WinApiTiming? timing = null, bool applyHygiene = true)
    {
        _timing = timing ?? new WinApiTiming();
        _applyHygiene = applyHygiene;
    }

    public async Task SendKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        IntPtr hwnd = RequireHandle(target);

        uint scan = NativeMethods.MapVirtualKeyW((uint)virtualKey, WinApiMessages.MAPVK_VK_TO_VSC);

        // T1 priming: fake activation so the engine accepts the key unfocused.
        Post(hwnd, WinApiMessages.WM_ACTIVATE, (IntPtr)WinApiMessages.WA_ACTIVE, IntPtr.Zero, "ACTIVATE");
        await Task.Delay(_timing.PrimeStepMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, WinApiMessages.WM_SETFOCUS, IntPtr.Zero, IntPtr.Zero, "SETFOCUS");
        await Task.Delay(_timing.PrimeStepMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, WinApiMessages.WM_ACTIVATEAPP, (IntPtr)1, IntPtr.Zero, "ACTIVATEAPP");
        await Task.Delay(_timing.PrimeStepMs, cancellationToken).ConfigureAwait(false);

        Post(hwnd, WinApiMessages.WM_KEYDOWN, (IntPtr)virtualKey, WinApiMessages.MakeKeyDownLParam(scan), "KEYDOWN");
        await Task.Delay(_timing.KeyHoldMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, WinApiMessages.WM_KEYUP, (IntPtr)virtualKey, WinApiMessages.MakeKeyUpLParam(scan), "KEYUP");

        if (_applyHygiene)
            ApplyHygiene(hwnd);
    }

    public async Task SendUiClickAsync(
        IWindowTarget target, double relativeX, double relativeY, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        IntPtr hwnd = RequireHandle(target);

        if (!NativeMethods.GetClientRect(hwnd, out NativeMethods.RECT rect))
            throw new WinApiException("GetClientRect failed.");

        int x = (int)(relativeX * (rect.Right - rect.Left));
        int y = (int)(relativeY * (rect.Bottom - rect.Top));
        IntPtr lParam = WinApiMessages.MakeLParam(x, y);

        // C4 mouse priming, then the click itself (all in client-area coords).
        Post(hwnd, WinApiMessages.WM_NCHITTEST, IntPtr.Zero, lParam, "HITTEST");
        await Task.Delay(_timing.MousePrimeStepMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, WinApiMessages.WM_MOUSEMOVE, IntPtr.Zero, lParam, "MOVE");
        await Task.Delay(_timing.MousePrimeStepMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, WinApiMessages.WM_SETCURSOR, hwnd,
            (IntPtr)((WinApiMessages.WM_LBUTTONDOWN << 16) | WinApiMessages.HTCLIENT), "SETCURSOR");
        await Task.Delay(_timing.MousePrimeStepMs, cancellationToken).ConfigureAwait(false);

        Post(hwnd, WinApiMessages.WM_LBUTTONDOWN, (IntPtr)WinApiMessages.MK_LBUTTON, lParam, "LBUTTONDOWN");
        await Task.Delay(_timing.ClickHoldMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, WinApiMessages.WM_LBUTTONUP, IntPtr.Zero, lParam, "LBUTTONUP");

        if (_applyHygiene)
            ApplyHygiene(hwnd);
    }

    private void ApplyHygiene(IntPtr hwnd)
    {
        Post(hwnd, WinApiMessages.WM_ACTIVATE, (IntPtr)WinApiMessages.WA_INACTIVE, IntPtr.Zero, "HYGIENE-ACTIVATE");
        Post(hwnd, WinApiMessages.WM_ACTIVATEAPP, IntPtr.Zero, IntPtr.Zero, "HYGIENE-ACTIVATEAPP");
    }

    private static IntPtr RequireHandle(IWindowTarget target) =>
        target.WindowHandle != IntPtr.Zero
            ? target.WindowHandle
            : throw new WinApiException($"No live window for account {target.AccountId}.");

    private static void Post(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, string tag)
    {
        if (!NativeMethods.PostMessageW(hwnd, message, wParam, lParam))
            throw new WinApiException($"PostMessage failed: {tag} (msg=0x{message:X}).");
    }
}
