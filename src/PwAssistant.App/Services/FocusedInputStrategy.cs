using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.App.Services;

/// <summary>
/// Dispatch decorator (Fase 4b + clean recipe, 2026-10-01): two opt-in
/// session/persisted modes over the legacy T1/C4 background recipe.
/// Focused mode (persisted): real foreground per account via
/// BringToFront + restore at batch end. Clean mode (session): bare
/// DOWN/UP sends with NO foreground call at all — our interactive process
/// holds the foreground grant (the Fire click itself), so even an unforced
/// SetForegroundWindow would be granted and visibly switch; the Helper's
/// sender never holds the grant (no input), hence flash-only there. A
/// lock-denied call changes nothing the game can observe, so bare sends
/// without poison (no fake priming, no WA_INACTIVE hygiene, suspect #1
/// for background skips) are the whole recipe. Sends stay PostMessage
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

    public Task SendKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default) =>
        SendWithFocusAsync(target,
            () => Clean
                ? _inner.SendKeyCleanAsync(target, virtualKey, cancellationToken)
                : _inner.SendKeyAsync(target, virtualKey, cancellationToken),
            cancellationToken);

    public Task SendUiClickAsync(
        IWindowTarget target, double relativeX, double relativeY,
        MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default) =>
        SendWithFocusAsync(target,
            () => Clean
                ? _inner.SendUiClickCleanAsync(target, relativeX, relativeY, button, cancellationToken)
                : _inner.SendUiClickAsync(target, relativeX, relativeY, button, cancellationToken),
            cancellationToken);

    private async Task SendWithFocusAsync(IWindowTarget target, Func<Task> send, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target.WindowHandle != IntPtr.Zero && target.WindowHandle != _lastHwnd)
        {
            if (Enabled)
            {
                if (!WindowFocus.BringToFront(target.WindowHandle))
                    _log.Warn($"Focused dispatch: could not foreground hwnd=0x{target.WindowHandle:X}.");
                _lastHwnd = target.WindowHandle;
                await Task.Delay(FocusSettleMs, cancellationToken).ConfigureAwait(false);
            }
            else if (Clean)
            {
                // No foreground call by design (see class docs): just the
                // per-account gap, then bare sends. Foreground is only
                // observed (read-only) for the verbose log.
                if (_state.VerboseFireLog)
                    _log.Info($"  [fg] hwnd=0x{target.WindowHandle:X} fg=0x{SafeForeground():X}");
                _lastHwnd = target.WindowHandle;
                await Task.Delay(CleanSettleMs, cancellationToken).ConfigureAwait(false);
            }
        }
        await send().ConfigureAwait(false);
    }

    public void BeginBatch()
    {
        _lastHwnd = IntPtr.Zero;
        _batchPreviousForeground = IntPtr.Zero;
        if (!Enabled) return;
        try
        {
            _batchPreviousForeground = _resolver.GetForegroundWindow();
        }
        catch
        {
            // Best effort: restore is a courtesy, never load-bearing.
        }
    }

    public void EndBatch()
    {
        _lastHwnd = IntPtr.Zero;
        // Clean mode moves nothing, so there is nothing to restore.
        if (!Enabled)
        {
            _batchPreviousForeground = IntPtr.Zero;
            return;
        }
        try
        {
            if (_batchPreviousForeground != IntPtr.Zero)
                WindowFocus.BringToFront(_batchPreviousForeground);
        }
        catch
        {
            // Best effort (see BeginBatch).
        }
        finally
        {
            _batchPreviousForeground = IntPtr.Zero;
        }
    }

    private IntPtr SafeForeground()
    {
        try
        {
            return _resolver.GetForegroundWindow();
        }
        catch
        {
            return IntPtr.Zero;
        }
    }
}
