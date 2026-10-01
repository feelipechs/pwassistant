using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.App.Services;

/// <summary>
/// Dispatch decorator (Fase 4b + clean recipe, 2026-10-01): two opt-in
/// session/persisted modes over the legacy T1/C4 background recipe.
/// Focused mode (persisted): real foreground per account via
/// BringToFront + restore at batch end. Clean mode (session): one real
/// (usually lock-blocked) SetForegroundWindow per account — the Helper's
/// confirmed recipe — then bare DOWN/UP sends, no fake priming, no
/// WA_INACTIVE hygiene (suspect #1 for background skips). Restore happens
/// only when this batch actually moved the foreground. Sends stay
/// PostMessage (no SendInput, no injection); only the foreground is real.
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
                // Helper mirror: one real SetForegroundWindow, no forcing.
                // Blocked (taskbar flashes) is the expected case, not a failure.
                bool granted = WindowFocus.TrySetSoft(target.WindowHandle);
                if (granted)
                    _batchMovedForeground = true;
                if (_state.VerboseFireLog)
                    _log.Info($"  [fg] hwnd=0x{target.WindowHandle:X} setfg={(granted ? 1 : 0)}");
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
}
