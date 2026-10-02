using System.Runtime.InteropServices;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;

namespace PwAssistant.WinApi;

/// <summary>One posted message with its queue-acceptance result (diagnostics only:
/// Ok=true proves delivery to the queue, never game effect).</summary>
public sealed record SendTrace(DateTimeOffset At, IntPtr Hwnd, string Tag, uint Message, bool Ok, int Win32Error);

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

    /// <summary>Clean-recipe timings (Helper mirror): no priming, no hold,
    /// UP glued right after DOWN. Never zeroed.</summary>
    private const int CleanSettleMs = 50;
    private const int CleanKeyHoldMs = 20;
    private const int CleanClickGapMs = 20;

    /// <summary>Raised once per posted message (diagnostics; null when nobody listens).</summary>
    public event Action<SendTrace>? Traced;

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
        IWindowTarget target, double relativeX, double relativeY,
        MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        IntPtr hwnd = RequireHandle(target);

        if (!NativeMethods.GetClientRect(hwnd, out NativeMethods.RECT rect))
            throw new WinApiException("GetClientRect failed.");

        int x = (int)(relativeX * (rect.Right - rect.Left));
        int y = (int)(relativeY * (rect.Bottom - rect.Top));
        IntPtr lParam = WinApiMessages.MakeLParam(x, y);

        bool right = button == MouseButton.Right;
        uint downMsg = right ? WinApiMessages.WM_RBUTTONDOWN : WinApiMessages.WM_LBUTTONDOWN;
        uint upMsg = right ? WinApiMessages.WM_RBUTTONUP : WinApiMessages.WM_LBUTTONUP;
        int mkButton = right ? WinApiMessages.MK_RBUTTON : WinApiMessages.MK_LBUTTON;

        // C4 mouse priming, then the click itself (all in client-area coords).
        Post(hwnd, WinApiMessages.WM_NCHITTEST, IntPtr.Zero, lParam, "HITTEST");
        await Task.Delay(_timing.MousePrimeStepMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, WinApiMessages.WM_MOUSEMOVE, IntPtr.Zero, lParam, "MOVE");
        await Task.Delay(_timing.MousePrimeStepMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, WinApiMessages.WM_SETCURSOR, hwnd,
            (IntPtr)((downMsg << 16) | WinApiMessages.HTCLIENT), "SETCURSOR");
        await Task.Delay(_timing.MousePrimeStepMs, cancellationToken).ConfigureAwait(false);

        Post(hwnd, downMsg, (IntPtr)mkButton, lParam, "BUTTONDOWN");
        await Task.Delay(_timing.ClickHoldMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, upMsg, IntPtr.Zero, lParam, "BUTTONUP");

        if (_applyHygiene)
            ApplyHygiene(hwnd);
    }

    private void ApplyHygiene(IntPtr hwnd)
    {
        Post(hwnd, WinApiMessages.WM_ACTIVATE, (IntPtr)WinApiMessages.WA_INACTIVE, IntPtr.Zero, "HYGIENE-ACTIVATE");
        Post(hwnd, WinApiMessages.WM_ACTIVATEAPP, IntPtr.Zero, IntPtr.Zero, "HYGIENE-ACTIVATEAPP");
    }

    /// <summary>Key hold for the worker-path direct send (Helper-v2 shape).</summary>
    private const int DirectKeyHoldMs = 50;

    /// <summary>
    /// Direct key (worker path, Helper-v2 shape): bare DOWN/UP with proper
    /// scan code — no fake priming, no hygiene, no hold games. The caller
    /// (a separate input-less sender process) owns the activation step
    /// (restore + lone SetForegroundWindow + settle), never this method.
    /// </summary>
    public async Task SendKeyDirectAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        IntPtr hwnd = RequireHandle(target);

        uint scan = NativeMethods.MapVirtualKeyW((uint)virtualKey, WinApiMessages.MAPVK_VK_TO_VSC);

        Post(hwnd, WinApiMessages.WM_KEYDOWN, (IntPtr)virtualKey, WinApiMessages.MakeKeyDownLParam(scan), "KEYDOWN");
        await Task.Delay(DirectKeyHoldMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, WinApiMessages.WM_KEYUP, (IntPtr)virtualKey, WinApiMessages.MakeKeyUpLParam(scan), "KEYUP");
    }

    /// <summary>
    /// Pure background key (Helper binary mirror, exact): KEYDOWN with
    /// lParam=0 and NOTHING else — no scan code, no hold, no KEYUP, no
    /// prime, no hygiene. The caller makes no foreground call either.
    /// SAFETY: a DOWN without UP latches the key in the game until any UP
    /// for the same VK arrives; recovery is one physical tap of the key.
    /// Test only on toggle skills first (mount/buffs), never on movement.
    /// </summary>
    public Task SendKeyPureAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        IntPtr hwnd = RequireHandle(target);
        cancellationToken.ThrowIfCancellationRequested();

        Post(hwnd, WinApiMessages.WM_KEYDOWN, (IntPtr)virtualKey, IntPtr.Zero, "KEYDOWN");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Clean key: bare DOWN/UP with proper scan code — no fake
    /// ACTIVATE/SETFOCUS priming, no WA_INACTIVE hygiene. The prime is a
    /// real (possibly lock-blocked) SetForegroundWindow call made by the
    /// caller, never a posted fake. Safer superset of the Helper (keeps UP
    /// + real lParam so keys never latch); exact mirror lives in
    /// <see cref="SendKeyPureAsync"/>.
    /// </summary>
    public async Task SendKeyCleanAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        IntPtr hwnd = RequireHandle(target);

        uint scan = NativeMethods.MapVirtualKeyW((uint)virtualKey, WinApiMessages.MAPVK_VK_TO_VSC);

        Post(hwnd, WinApiMessages.WM_KEYDOWN, (IntPtr)virtualKey, WinApiMessages.MakeKeyDownLParam(scan), "KEYDOWN");
        await Task.Delay(CleanKeyHoldMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, WinApiMessages.WM_KEYUP, (IntPtr)virtualKey, WinApiMessages.MakeKeyUpLParam(scan), "KEYUP");
    }

    /// <summary>
    /// Clean click (Helper mirror): MOVE, gap, DOWN, UP glued. No HITTEST,
    /// no SETCURSOR, no hold, no hygiene. Client-area coords like legacy.
    /// </summary>
    public async Task SendUiClickCleanAsync(
        IWindowTarget target, double relativeX, double relativeY,
        MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        IntPtr hwnd = RequireHandle(target);

        if (!NativeMethods.GetClientRect(hwnd, out NativeMethods.RECT rect))
            throw new WinApiException("GetClientRect failed.");

        int x = (int)(relativeX * (rect.Right - rect.Left));
        int y = (int)(relativeY * (rect.Bottom - rect.Top));
        IntPtr lParam = WinApiMessages.MakeLParam(x, y);

        bool right = button == MouseButton.Right;
        uint downMsg = right ? WinApiMessages.WM_RBUTTONDOWN : WinApiMessages.WM_LBUTTONDOWN;
        uint upMsg = right ? WinApiMessages.WM_RBUTTONUP : WinApiMessages.WM_LBUTTONUP;
        int mkButton = right ? WinApiMessages.MK_RBUTTON : WinApiMessages.MK_LBUTTON;

        Post(hwnd, WinApiMessages.WM_MOUSEMOVE, IntPtr.Zero, lParam, "MOVE");
        await Task.Delay(CleanClickGapMs, cancellationToken).ConfigureAwait(false);
        Post(hwnd, downMsg, (IntPtr)mkButton, lParam, "BUTTONDOWN");
        Post(hwnd, upMsg, IntPtr.Zero, lParam, "BUTTONUP");
    }

    private static IntPtr RequireHandle(IWindowTarget target) =>
        target.WindowHandle != IntPtr.Zero
            ? target.WindowHandle
            : throw new WinApiException($"No live window for account {target.AccountId}.");

    private void Post(IntPtr hwnd, uint message, IntPtr wParam, IntPtr lParam, string tag)
    {
        bool ok = NativeMethods.PostMessageW(hwnd, message, wParam, lParam);
        int error = ok ? 0 : Marshal.GetLastWin32Error();
        Traced?.Invoke(new SendTrace(DateTimeOffset.UtcNow, hwnd, tag, message, ok, error));
        if (!ok)
            throw new WinApiException($"PostMessage failed: {tag} (msg=0x{message:X}, win32={error}).");
    }
}
