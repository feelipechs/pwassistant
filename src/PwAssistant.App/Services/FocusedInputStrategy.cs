using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.App.Services;

/// <summary>
/// Dispatch decorator (worker default, 2026-10-02): per-account action
/// batches flushed through the separate sender process (which never
/// receives user input, so its lone SetForegroundWindow is denied on
/// normal machines — background, Helper parity). Lock-disabled machines
/// (ForegroundLockTimeout near zero: every call granted) fall back to
/// legacy in-process sends with no focus call at all. Either way the sends
/// are bare (no fake priming, no WA_INACTIVE hygiene) and NOTHING in this
/// process ever calls SetForegroundWindow (no prime, no restore).
/// Retired 2026-10-02: forced (BringToFront), clean (soft call) and pure
/// (exact mirror) experiment modes — explicit switching stays available
/// via B4 (FocusController); dispatch itself is always background. Sends
/// stay PostMessage (no SendInput, no injection).
/// </summary>
public sealed class FocusedInputStrategy : IInputStrategy
{
    private readonly PostMessageBackgroundStrategy _inner;
    private readonly AppState _state;
    private readonly FileLogger _log;
    private readonly Func<Guid, int?> _pidResolver;
    private readonly SenderRunner _senderRunner;
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

    public event Action<SendTrace>? Traced
    {
        add => _inner.Traced += value;
        remove => _inner.Traced -= value;
    }

    public FocusedInputStrategy(
        PostMessageBackgroundStrategy inner, AppState state,
        FileLogger log, Func<Guid, int?> pidResolver, SenderRunner senderRunner)
    {
        _inner = inner;
        _state = state;
        _log = log;
        _pidResolver = pidResolver ?? throw new ArgumentNullException(nameof(pidResolver));
        _senderRunner = senderRunner ?? throw new ArgumentNullException(nameof(senderRunner));
    }

    /// <summary>
    /// Default path: buffer per account and flush through the separate
    /// sender process. Only inside a dispatcher batch (SyncController's
    /// live mirror clicks bypass buffering and stay immediate). Never on
    /// lock-disabled machines (where every call is granted: legacy
    /// in-process sends with no focus call at all are the only
    /// switch-free option).
    /// </summary>
    private bool WorkerActive =>
        _batchActive && !WindowDiagnostics.IsForegroundLockDisabled(_lockTimeoutMs);

    public Task SendKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default)
    {
        if (!WorkerActive)
            return _inner.SendKeyAsync(target, virtualKey, cancellationToken);
        WorkerBatch? batch = EnsureWorkerBatch(target);
        if (batch is null)
            return _inner.SendKeyAsync(target, virtualKey, cancellationToken);
        batch.AddKey(virtualKey);
        return Task.CompletedTask;
    }

    public Task SendUiClickAsync(
        IWindowTarget target, double relativeX, double relativeY,
        MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        if (!WorkerActive)
            return _inner.SendUiClickAsync(target, relativeX, relativeY, button, cancellationToken);
        WorkerBatch? batch = EnsureWorkerBatch(target);
        if (batch is null)
            return _inner.SendUiClickAsync(target, relativeX, relativeY, button, cancellationToken);
        batch.AddClick(relativeX, relativeY, button);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Worker buffering: actions accumulate per account and flush once per
    /// AccountAction through the sender process (see <see cref="FlushAsync"/>).
    /// Accounts with no known PID fall back to immediate legacy sends
    /// (same as the pre-worker default path).
    /// </summary>
    private WorkerBatch? EnsureWorkerBatch(IWindowTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
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

    public void BeginBatch()
    {
        lock (_worker)
        {
            _worker.Clear();
        }
        _batchActive = true;
        // Fresh per fire: the lock timeout is volatile by design (any
        // process may rewrite it at runtime), so a startup sample would lie.
        _lockTimeoutMs = WindowDiagnostics.GetForegroundLockTimeoutMs();
        string mode = WindowDiagnostics.IsForegroundLockDisabled(_lockTimeoutMs) ? "legacy-anomaly" : "worker";
        _log.Info($"  [mode] {mode} timeoutMs={_lockTimeoutMs}");
        if (mode == "worker" && AcquireForegroundLock())
            _log.Info($"  [lock] ok=1 win32=0");
        else if (mode == "worker")
            _log.Info($"  [lock] ok=0 win32={_lockWin32}");
    }

    /// <summary>
    /// Flushes any leftover worker batches (normally empty: the executor
    /// flushes per account) and releases the foreground lock. Never
    /// restores the previous foreground: the restore call itself comes
    /// from our input-latched process and would visibly yank focus back,
    /// which is exactly what background parity forbids (the Helper has no
    /// restore either).
    /// </summary>
    public async Task EndBatchAsync(CancellationToken cancellationToken = default)
    {
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
        }
        catch
        {
            // Best effort (see BeginBatch).
        }
        finally
        {
            _batchActive = false;
            // Release AFTER leftover flushes ran under it.
            ReleaseForegroundLock();
        }
    }

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
}
