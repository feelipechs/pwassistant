using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.App.Services;

/// <summary>
/// Focused dispatch decorator (Fase 4b): when enabled, brings each account
/// window to real foreground before its sends — proven necessary on loaded
/// machines where the engine ignores background PostMessage — and restores
/// the previous foreground at batch end. Sends stay PostMessage (no
/// SendInput, no injection); only the foreground is real.
/// </summary>
public sealed class FocusedInputStrategy : IInputStrategy
{
    /// <summary>Breathing room after BringToFront before the first send.</summary>
    private const int FocusSettleMs = 200;

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

    public Task SendKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default) =>
        SendWithFocusAsync(target, () => _inner.SendKeyAsync(target, virtualKey, cancellationToken), cancellationToken);

    public Task SendUiClickAsync(
        IWindowTarget target, double relativeX, double relativeY,
        MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default) =>
        SendWithFocusAsync(target, () => _inner.SendUiClickAsync(target, relativeX, relativeY, button, cancellationToken), cancellationToken);

    private async Task SendWithFocusAsync(IWindowTarget target, Func<Task> send, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Enabled && target.WindowHandle != IntPtr.Zero && target.WindowHandle != _lastHwnd)
        {
            if (!WindowFocus.BringToFront(target.WindowHandle))
                _log.Warn($"Focused dispatch: could not foreground hwnd=0x{target.WindowHandle:X}.");
            _lastHwnd = target.WindowHandle;
            await Task.Delay(FocusSettleMs, cancellationToken).ConfigureAwait(false);
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
        if (!Enabled) return;
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
}
