using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.Avalonia.Services;

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

    /// <summary>
    /// Per-fire batch state. The strategy is a singleton but fires overlap
    /// (several loops + manual fires), so mode, buffers and lock ownership
    /// must belong to the fire, never to the strategy: otherwise one fire
    /// ending flips another one mid-flight onto the legacy in-process path
    /// (fake prime + WA_INACTIVE hygiene), clears its buffers, or unlocks
    /// under it. Flows through the async call chain of FireAsync only.
    /// </summary>
    private sealed class BatchScope
    {
        public bool Worker { get; init; }
        public bool HoldsLock { get; set; }
        public Dictionary<Guid, WorkerBatch?> Batches { get; } = new();
    }

    private readonly AsyncLocal<BatchScope?> _scope = new();
    private int _activeFires;

    /// <summary>
    /// Test seam: pins the volatile OS foreground-lock timeout sample so
    /// worker/legacy routing is deterministic under test. Null (default)
    /// reads the live OS value. Set once before any batch, never per fire.
    /// </summary>
    internal uint? TestLockTimeoutMs { get; set; }

    /// <summary>
    /// System-wide foreground lock nesting: LockSetForegroundWindow is a
    /// global on/off switch, not refcounted — overlapping fires (loop +
    /// manual) must not let the first EndBatch unlock under the second.
    /// </summary>
    private int _lockHolds;

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
    /// switch-free option). Calls made outside a fire (SyncController's
    /// live mirror clicks) have no scope and are always immediate.
    /// </summary>
    private BatchScope? ActiveScope => _scope.Value is { Worker: true } scope ? scope : null;

    public Task SendKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default)
    {
        BatchScope? scope = ActiveScope;
        if (scope is null)
            return _inner.SendKeyAsync(target, virtualKey, cancellationToken);
        WorkerBatch? batch = EnsureWorkerBatch(scope, target);
        if (batch is null)
            return _inner.SendKeyAsync(target, virtualKey, cancellationToken);
        batch.AddKey(virtualKey);
        return Task.CompletedTask;
    }

    public Task SendUiClickAsync(
        IWindowTarget target, double relativeX, double relativeY,
        MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default)
    {
        BatchScope? scope = ActiveScope;
        if (scope is null)
            return _inner.SendUiClickAsync(target, relativeX, relativeY, button, cancellationToken);
        WorkerBatch? batch = EnsureWorkerBatch(scope, target);
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
    private WorkerBatch? EnsureWorkerBatch(BatchScope scope, IWindowTarget target)
    {
        ArgumentNullException.ThrowIfNull(target);
        lock (scope.Batches)
        {
            if (scope.Batches.TryGetValue(target.AccountId, out WorkerBatch? existing))
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
            scope.Batches[target.AccountId] = batch;
            return batch;
        }
    }

    public Task SleepAsync(IWindowTarget target, int millisecondsDelay, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(target);
        BatchScope? scope = ActiveScope;
        if (scope is not null)
        {
            WorkerBatch? batch = EnsureWorkerBatch(scope, target);
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
        BatchScope? scope = ActiveScope;
        if (scope is null)
            return;
        WorkerBatch? batch;
        lock (scope.Batches)
        {
            if (!scope.Batches.TryGetValue(target.AccountId, out batch) || batch is null || batch.IsEmpty)
                return;
        }
        await FlushBatchAsync(scope, target.AccountId, batch, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// One sender invocation per account. A flush failure throws, so the
    /// executor attributes it to this account through skip-with-error —
    /// same as an in-process send failure. The batch is consumed even on
    /// failure (never double-sent). Spawn cost doubles as the
    /// inter-account gap, so no extra delay here.
    /// </summary>
    private async Task FlushBatchAsync(BatchScope scope, Guid accountId, WorkerBatch batch, CancellationToken cancellationToken)
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
            lock (scope.Batches)
            {
                scope.Batches.Remove(accountId);
            }
        }
    }

    /// <summary>
    /// Opens this fire's own batch scope (call from the fire's async flow;
    /// the scope never leaks to the caller's context nor to other fires).
    /// </summary>
    public void BeginBatch()
    {
        // Fresh per fire: the lock timeout is volatile by design (any
        // process may rewrite it at runtime), so a startup sample would lie.
        // Tests pin it via TestLockTimeoutMs for deterministic routing.
        uint timeoutMs = TestLockTimeoutMs ?? WindowDiagnostics.GetForegroundLockTimeoutMs();
        bool worker = !WindowDiagnostics.IsForegroundLockDisabled(timeoutMs);
        var scope = new BatchScope { Worker = worker };
        _scope.Value = scope;

        int fires = Interlocked.Increment(ref _activeFires);
        _log.Info($"  [mode] {(worker ? "worker" : "legacy-anomaly")} timeoutMs={timeoutMs} fires={fires}");
        if (!worker)
            return;
        scope.HoldsLock = AcquireForegroundLock(out int win32);
        _log.Info($"  [lock] ok={(scope.HoldsLock ? 1 : 0)} win32={win32}");
    }

    /// <summary>
    /// Flushes any leftover worker batches of THIS fire (normally empty:
    /// the executor flushes per account) and releases the foreground lock
    /// this fire took. Never restores the previous foreground: the restore
    /// call itself comes from our input-latched process and would visibly
    /// yank focus back, which is exactly what background parity forbids
    /// (the Helper has no restore either).
    /// </summary>
    public async Task EndBatchAsync(CancellationToken cancellationToken = default)
    {
        BatchScope? scope = _scope.Value;
        if (scope is null)
            return;
        try
        {
            List<KeyValuePair<Guid, WorkerBatch?>> leftovers;
            lock (scope.Batches)
            {
                leftovers = new List<KeyValuePair<Guid, WorkerBatch?>>(scope.Batches);
                scope.Batches.Clear();
            }
            foreach (KeyValuePair<Guid, WorkerBatch?> entry in leftovers)
            {
                if (entry.Value is not { IsEmpty: false } batch)
                    continue;
                try
                {
                    await FlushBatchAsync(scope, entry.Key, batch, cancellationToken).ConfigureAwait(false);
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
            _scope.Value = null;
            Interlocked.Decrement(ref _activeFires);
            // Release AFTER leftover flushes ran under it, and only the
            // hold this fire actually took.
            if (scope.HoldsLock)
            {
                scope.HoldsLock = false;
                ReleaseForegroundLock();
            }
        }
    }

    /// <summary>
    /// Takes one hold on the system foreground lock (first holder calls
    /// the OS; nested fires share it). A failed take releases its slot
    /// immediately: degraded mode is today's behavior, never an abort.
    /// </summary>
    private bool AcquireForegroundLock(out int win32Error)
    {
        win32Error = 0;
        if (Interlocked.Increment(ref _lockHolds) > 1)
            return true;
        bool ok = WindowFocus.TryLockForeground(out win32Error);
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
