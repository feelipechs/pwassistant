using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.App.Services;

/// <summary>
/// Dispatch decorator (Fase 4b + clean recipe, 2026-10-01): two opt-in
/// session/persisted modes over the legacy T1/C4 background recipe.
/// Focused mode (persisted): real foreground per account via
/// BringToFront + restore at batch end. Clean mode (session): Helper
/// structure (R2) — clicks never touch the foreground; every key gets one
/// real, unforced SetForegroundWindow with NO accompanying input (denied +
/// flash-only without the grant is the background case; granted + switch
/// with it). Pure-keys variant (session): exact binary mirror — KEYDOWN
/// lParam=0, no UP, and no focus call at all, so no switch is structurally
/// possible. Either way the sends are bare (no fake priming, no WA_INACTIVE
/// hygiene). Verbose per-send line carries the discriminators (grant result,
/// target thread gui active/focus, pump round-trip). Sends stay PostMessage
/// (no SendInput, no injection).
/// </summary>
public sealed class FocusedInputStrategy : IInputStrategy
{
    /// <summary>Breathing room after forced BringToFront before first send.</summary>
    private const int FocusSettleMs = 200;

    /// <summary>Gap after the soft (possibly blocked) SetForeground call.</summary>
    private const int CleanSettleMs = 50;

    private readonly PostMessageBackgroundStrategy _inner;
    private readonly AppState _state;
    private readonly IWindowResolver _resolver;
    private readonly FileLogger _log;
    private IntPtr _lastHwnd = IntPtr.Zero;
    private IntPtr _batchPreviousForeground = IntPtr.Zero;
    private bool _batchMovedForeground;

    public event Action<SendTrace>? Traced
    {
        add => _inner.Traced += value;
        remove => _inner.Traced -= value;
    }

    public FocusedInputStrategy(
        PostMessageBackgroundStrategy inner, AppState state,
        IWindowResolver resolver, FileLogger log)
    {
        _inner = inner;
        _state = state;
        _resolver = resolver;
        _log = log;
    }

    public bool Enabled => _state.Data.FocusedDispatch;
    private bool Clean => _state.CleanDispatch;
    /// <summary>Pure background keys: exact binary mirror, no focus call at
    /// all. Only meaningful with Clean on (falls back to legacy otherwise).</summary>
    private bool PureKeys => Clean && _state.PureBackgroundKeys;

    public Task SendKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default)
    {
        if (PureKeys)
            return SendPureKeyAsync(target, virtualKey, cancellationToken);
        return SendWithFocusAsync(target,
            () => Clean
                ? _inner.SendKeyCleanAsync(target, virtualKey, cancellationToken)
                : _inner.SendKeyAsync(target, virtualKey, cancellationToken),
            touchForeground: Enabled || Clean,
            cancellationToken);
    }

    public Task SendUiClickAsync(
        IWindowTarget target, double relativeX, double relativeY,
        MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default) =>
        SendWithFocusAsync(target,
            () => Clean
                ? _inner.SendUiClickCleanAsync(target, relativeX, relativeY, button, cancellationToken)
                : _inner.SendUiClickAsync(target, relativeX, relativeY, button, cancellationToken),
            // Helper structure (R2): clicks never touch the foreground;
            // only keys do. Forced mode keeps focusing both (unchanged).
            touchForeground: Enabled,
            cancellationToken);

    /// <summary>Pure path: straight to the exact send, no focus call,
    /// no gap, no restore. Nothing exists here for the lock to grant.</summary>
    private async Task SendPureKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (_state.VerboseFireLog && target.WindowHandle != IntPtr.Zero)
            LogProbe("pure", target.WindowHandle, null);
        await _inner.SendKeyPureAsync(target, virtualKey, cancellationToken).ConfigureAwait(false);
    }

    private async Task SendWithFocusAsync(
        IWindowTarget target, Func<Task> send, bool touchForeground,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        // Forced mode focuses once per account; clean keys call per action
        // (the binary primes every key, consecutive ones included).
        bool touch = touchForeground && target.WindowHandle != IntPtr.Zero
            && (Enabled ? target.WindowHandle != _lastHwnd : true);
        bool moved = false;
        if (touch)
        {
            if (Enabled)
            {
                moved = WindowFocus.BringToFront(target.WindowHandle);
                if (!moved)
                    _log.Warn($"Focused dispatch: could not foreground hwnd=0x{target.WindowHandle:X}.");
                await Task.Delay(FocusSettleMs, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                // One real, unforced SetForegroundWindow, no input alongside.
                // Blocked (taskbar flashes, setfg=0) is the expected
                // background case; granted (setfg=1) visibly switches.
                moved = WindowFocus.TrySetSoft(target.WindowHandle);
                if (moved)
                    _batchMovedForeground = true;
                await Task.Delay(CleanSettleMs, cancellationToken).ConfigureAwait(false);
            }
            _lastHwnd = target.WindowHandle;
        }
        if (_state.VerboseFireLog && (Enabled || Clean))
            LogProbe(touch ? "fg" : "bg", target.WindowHandle, touch ? moved : (bool?)null);
        await send().ConfigureAwait(false);
    }

    /// <summary>Per-send discriminator line (verbose only): grant result +
    /// the target thread's own active/focus view + pump round-trip.</summary>
    private void LogProbe(string tag, IntPtr hwnd, bool? granted)
    {
        try
        {
            GuiState gui = WindowDiagnostics.GetGuiState(hwnd);
            int pumpMs = WindowDiagnostics.ProbePumpMs(hwnd);
            string grant = granted.HasValue ? $" setfg={(granted.Value ? 1 : 0)}" : string.Empty;
            _log.Info($"  [{tag}] hwnd=0x{hwnd:X}{grant}"
                + $" gui=0x{gui.ActiveWindow:X} focus=0x{gui.FocusWindow:X} pumpMs={pumpMs}");
        }
        catch
        {
            // Diagnostics never break dispatch.
        }
    }

    public void BeginBatch()
    {
        _lastHwnd = IntPtr.Zero;
        _batchPreviousForeground = IntPtr.Zero;
        _batchMovedForeground = false;
        if (!Enabled && !Clean) return;
        try
        {
            _batchPreviousForeground = _resolver.GetForegroundWindow();
        }
        catch
        {
            // Best effort: restore is a courtesy, never load-bearing.
        }
        // Grant-diagnosis context (verbose only): who holds the foreground
        // as the batch starts, plus our own debugger/elevation state.
        if (_state.VerboseFireLog)
        {
            string owner = WindowFocus.GetOwnerProcessName(_batchPreviousForeground) ?? "-";
            _log.Info($"  [batch] prev=0x{_batchPreviousForeground:X} owner={owner}" +
                $" dbg={(System.Diagnostics.Debugger.IsAttached ? 1 : 0)} admin={(IsElevated() ? 1 : 0)}");
        }
    }

    public void EndBatch()
    {
        _lastHwnd = IntPtr.Zero;
        try
        {
            if (_batchPreviousForeground == IntPtr.Zero)
                return;
            if (Enabled)
                WindowFocus.BringToFront(_batchPreviousForeground);
            else if (_batchMovedForeground)
                WindowFocus.TrySetSoft(_batchPreviousForeground);
        }
        catch
        {
            // Best effort (see BeginBatch).
        }
        finally
        {
            _batchPreviousForeground = IntPtr.Zero;
            _batchMovedForeground = false;
        }
    }

    /// <summary>Read-only self check for the grant-diagnosis line.
    /// False on any error (unknown reads as non-elevated, never as granted).</summary>
    private static bool IsElevated()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            return new System.Security.Principal.WindowsPrincipal(identity)
                .IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }
}
