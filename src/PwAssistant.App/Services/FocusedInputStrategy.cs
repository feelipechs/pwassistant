using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.App.Services;

/// <summary>
/// Dispatch decorator (Fase 4b + clean recipe + worker, 2026-10-02): modes
/// over the legacy T1/C4 background recipe.
/// Focused mode (persisted): real foreground per account via
/// BringToFront + restore at batch end. Clean mode (session): Helper
/// structure (R2) — clicks never touch the foreground; every key gets one
/// real, unforced SetForegroundWindow with NO accompanying input (denied +
/// flash-only without the grant is the background case; granted + switch
/// with it). Pure-keys variant (session): exact binary mirror — KEYDOWN
/// lParam=0, no UP, and no focus call at all, so no switch is structurally
/// possible. Worker mode (default): per-account action batches flushed
/// through the separate sender process (which never receives user input,
/// so its lone SetForegroundWindow is denied on normal machines —
/// background, Helper parity). Lock-disabled machines (ForegroundLockTimeout
/// near zero: every call granted) fall back to legacy in-process sends
/// with no focus call at all. Either way the sends are bare (no fake
/// priming, no WA_INACTIVE hygiene). Verbose per-send line carries the
/// discriminators (grant result, target thread gui active/focus, pump
/// round-trip). Sends stay PostMessage (no SendInput, no injection).
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
    private readonly Func<Guid, int?> _pidResolver;
    private readonly SenderRunner _senderRunner;
    private IntPtr _lastHwnd = IntPtr.Zero;
    private IntPtr _batchPreviousForeground = IntPtr.Zero;
    private bool _batchActive;
    private uint _lockTimeoutMs = uint.MaxValue;
    private readonly Dictionary<Guid, WorkerBatch?> _worker = new();

    /// <summary>
    /// System-wide foreground lock nesting: LockSetForegroundWindow is a
    /// global on/off switch, not refcounted — overlapping fires (loop +
    /// manual) must not let the first EndBatch unlock under the second.
    /// </summary>
    private int _lockHolds;

    private int _lockWin32;

    /// <summary>
    /// Takes one hold on the system foreground lock (first holder calls
    /// the OS; nested fires share it). A failed take releases its slot
    /// immediately: degraded mode is today's behavior, never an abort.
    /// </summary>
    private bool AcquireForegroundLock()
    {
        if (Interlocked.Increment(ref _lockHolds) > 1)
        {
            _lockWin32 = 0;
            return true;
        }
        bool ok = WindowFocus.TryLockForeground(out int win32);
        _lockWin32 = win32;
        if (!ok)
            Interlocked.Decrement(ref _lockHolds);
        return ok;
    }

    /// <summary>
    /// Releases one hold; the OS unlock runs only when the last holder
    /// leaves. Never unlocks what we didn't lock (underflow clamps).
    /// </summary>
    private void ReleaseForegroundLock()
    {
        int remaining = Interlocked.Decrement(ref _lockHolds);
        if (remaining > 0)
            return;
        if (remaining < 0)
        {
            Interlocked.Exchange(ref _lockHolds, 0);
            return;
        }
        WindowFocus.UnlockForeground();
    }

    public event Action<SendTrace>? Traced
    {
        add => _inner.Traced += value;
        remove => _inner.Traced -= value;
    }

    public FocusedInputStrategy(
        PostMessageBackgroundStrategy inner, AppState state,
        IWindowResolver resolver, FileLogger log,
        Func<Guid, int?> pidResolver, SenderRunner senderRunner)
    {
        _inner = inner;
        _state = state;
        _resolver = resolver;
        _log = log;
        _pidResolver = pidResolver ?? throw new ArgumentNullException(nameof(pidResolver));
        _senderRunner = senderRunner ?? throw new ArgumentNullException(nameof(senderRunner));
    }

    public bool Enabled => _state.Data.FocusedDispatch;
    private bool Clean => _state.CleanDispatch;
    /// <summary>Pure background keys: exact binary mirror, no focus call at
    /// all. Only meaningful with Clean on (falls back to legacy otherwise).</summary>
    private bool PureKeys => Clean && _state.PureBackgroundKeys;

    /// <summary>
    /// Default path: buffer per account and flush through the separate
    /// sender process. Only inside a dispatcher batch (SyncController's
    /// live mirror clicks bypass buffering and stay immediate). Never when
    /// the user opted into forced/clean, and never on lock-disabled
    /// machines (where every call is granted: legacy in-process sends with
    /// no focus call at all are the only switch-free option).
    /// </summary>
    private bool WorkerActive =>
        _batchActive && !Enabled && !Clean
        && !WindowDiagnostics.IsForegroundLockDisabled(_lockTimeoutMs);

    public Task SendKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default)
    {
        if (PureKeys)
            return SendPureKeyAsync(target, virtualKey, cancellationToken);
        if (WorkerActive)
            return SendKeyWorkerAsync(target, virtualKey, cancellationToken);
        return SendWithFocusAsync(target,
            () => Clean
                ? _inner.SendKeyCleanAsync(target, virtualKey, cancellationToken)
                : _inner.SendKeyAsync(target, virtualKey, cancellationToken),
            touchForeground: Enabled || Clean,
            cancellationToken);
    }

    public Task SendUiClickAsync(
        IWindowTarget target, double relativeX, double relativeY,
        MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        if (WorkerActive)
            return SendClickWorkerAsync(target, relativeX, relativeY, button, cancellationToken);
        return SendWithFocusAsync(target,
            () => Clean
                ? _inner.SendUiClickCleanAsync(target, relativeX, relativeY, button, cancellationToken)
                : _inner.SendUiClickAsync(target, relativeX, relativeY, button, cancellationToken),
            // Helper structure (R2): clicks never touch the foreground;
            // only keys do. Forced mode keeps focusing both (unchanged).
            touchForeground: Enabled,
            cancellationToken);
    }

    /// <summary>
    /// Worker buffering: actions accumulate per account and flush once per
    /// AccountAction through the sender process (see <see cref="FlushAsync"/>).
    /// Accounts with no known PID fall back to immediate legacy sends
    /// (same as today's default path).
    /// </summary>
    private Task SendKeyWorkerAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        WorkerBatch? batch = EnsureWorkerBatch(target);
        if (batch is null)
            return SendWithFocusAsync(target,
                () => _inner.SendKeyAsync(target, virtualKey, cancellationToken),
                touchForeground: false, cancellationToken);
        batch.AddKey(virtualKey);
        return Task.CompletedTask;
    }

    private Task SendClickWorkerAsync(
        IWindowTarget target, double relativeX, double relativeY,
        MouseButton button, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(target);
        WorkerBatch? batch = EnsureWorkerBatch(target);
        if (batch is null)
            return SendWithFocusAsync(target,
                () => _inner.SendUiClickAsync(target, relativeX, relativeY, button, cancellationToken),
                touchForeground: false, cancellationToken);
        batch.AddClick(relativeX, relativeY, button);
        return Task.CompletedTask;
    }

    private WorkerBatch? EnsureWorkerBatch(IWindowTarget target)
    {
        lock (_worker)
        {
            if (_worker.TryGetValue(target.AccountId, out WorkerBatch? existing))
                return existing;
            int? pid = null;
            try
            {
                pid = _pidResolver(target.AccountId);
            }
            catch
            {
                // PID lookup is best effort; unknown falls back to legacy.
            }
            WorkerBatch? batch = pid.HasValue ? new WorkerBatch(pid.Value) : null;
            _worker[target.AccountId] = batch;
            return batch;
        }
    }

    public Task SleepAsync(IWindowTarget target, int millisecondsDelay, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (WorkerActive)
        {
            WorkerBatch? batch = EnsureWorkerBatch(target);
            if (batch is not null)
            {
                batch.AddSleep(millisecondsDelay);
                return Task.CompletedTask;
            }
        }
        return Task.Delay(millisecondsDelay, cancellationToken);
    }

    public async Task FlushAsync(IWindowTarget target, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (!WorkerActive)
            return;
        WorkerBatch? batch;
        lock (_worker)
        {
            if (!_worker.TryGetValue(target.AccountId, out batch) || batch is null || batch.IsEmpty)
                return;
        }
        await FlushBatchAsync(target.AccountId, batch, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// One sender invocation per account. A flush failure throws, so the
    /// executor attributes it to this account through skip-with-error —
    /// same as an in-process send failure. The batch is consumed even on
    /// failure (never double-sent). Spawn cost doubles as the
    /// inter-account gap, so no extra delay here.
    /// </summary>
    private async Task FlushBatchAsync(Guid accountId, WorkerBatch batch, CancellationToken cancellationToken)
    {
        // Never spawn a sender just to kill it: a canceled batch stays
        // unflushed (the fire is already tearing down).
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            SenderWorkload workload = batch.ToWorkload(_state.VerboseFireLog);
            SenderResult result = await _senderRunner(workload, cancellationToken).ConfigureAwait(false);
            if (!result.Ok)
                throw new WinApiException(
                    $"sender pid={batch.ProcessId}: {result.Error ?? $"failed={result.Failed}"} (sent={result.Sent})");
        }
        finally
        {
            lock (_worker)
            {
                _worker.Remove(accountId);
            }
        }
    }

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
        lock (_worker)
        {
            _worker.Clear();
        }
        _batchActive = true;
        // Fresh per fire: the lock timeout is volatile by design (any
        // process may rewrite it at runtime), so a startup sample would lie.
        _lockTimeoutMs = WindowDiagnostics.GetForegroundLockTimeoutMs();
        string mode = Enabled ? "forced"
            : Clean ? "clean"
            : WindowDiagnostics.IsForegroundLockDisabled(_lockTimeoutMs) ? "legacy-anomaly"
            : "worker";
        _log.Info($"  [mode] {mode} timeoutMs={_lockTimeoutMs}");
        if (mode == "worker" && AcquireForegroundLock())
            _log.Info($"  [lock] ok=1 win32=0");
        else if (mode == "worker")
            _log.Info($"  [lock] ok=0 win32={_lockWin32}");
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

    /// <summary>
    /// Flushes any leftover worker batches (normally empty: the executor
    /// flushes per account), then restores the previous foreground — but
    /// ONLY in forced mode. Clean/worker never restore: the restore call
    /// itself comes from our input-latched process and would visibly yank
    /// focus back, which is exactly what background parity forbids
    /// (the Helper has no restore either).
    /// </summary>
    public async Task EndBatchAsync(CancellationToken cancellationToken = default)
    {
        _lastHwnd = IntPtr.Zero;
        try
        {
            List<KeyValuePair<Guid, WorkerBatch?>> leftovers;
            lock (_worker)
            {
                leftovers = new List<KeyValuePair<Guid, WorkerBatch?>>(_worker);
                _worker.Clear();
            }
            foreach (KeyValuePair<Guid, WorkerBatch?> entry in leftovers)
            {
                if (entry.Value is not { IsEmpty: false } batch)
                    continue;
                try
                {
                    await FlushBatchAsync(entry.Key, batch, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Leftovers only exist after a mid-account failure (whose
                    // own skip-with-error already stands), so this is logged,
                    // never rethrown into the dispatcher finally.
                    _log.Warn($"Leftover worker flush failed: {ex.Message}");
                }
            }
            if (_batchPreviousForeground == IntPtr.Zero)
                return;
            if (Enabled)
                WindowFocus.BringToFront(_batchPreviousForeground);
        }
        catch
        {
            // Best effort (see BeginBatch).
        }
        finally
        {
            _batchPreviousForeground = IntPtr.Zero;
            _batchActive = false;
            // Release AFTER leftover flushes ran under it (and after any
            // forced restore, which never holds the lock anyway).
            ReleaseForegroundLock();
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
