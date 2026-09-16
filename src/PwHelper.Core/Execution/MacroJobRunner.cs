using PwHelper.Core.Models;

namespace PwHelper.Core.Execution;

/// <summary>
/// Cooperative cancellation by jobId: execute / cancel / cancelAll.
/// Cancellation applies between actions — never inside a DOWN/UP pair
/// (the pair is atomic inside the strategy).
/// </summary>
public sealed class MacroJobRunner : IDisposable
{
    private readonly Func<Preset, Guid, CancellationToken, Task<PresetExecutionResult>> _execute;
    private readonly Dictionary<Guid, CancellationTokenSource> _jobs = new();
    private readonly object _gate = new();
    private bool _disposed;

    public MacroJobRunner(Func<Preset, Guid, CancellationToken, Task<PresetExecutionResult>> execute)
    {
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));
    }

    public async Task<PresetExecutionResult> ExecuteAsync(Preset preset, CancellationToken callerToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);
        var jobId = Guid.NewGuid();
        var cts = CancellationTokenSource.CreateLinkedTokenSource(callerToken);

        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _jobs[jobId] = cts;
        }

        try
        {
            return await _execute(preset, jobId, cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return new PresetExecutionResult(jobId, preset.Id, Canceled: true, Array.Empty<AccountExecutionResult>());
        }
        finally
        {
            lock (_gate)
                _jobs.Remove(jobId);
            cts.Dispose();
        }
    }

    public void Cancel(Guid jobId)
    {
        lock (_gate)
        {
            if (_jobs.TryGetValue(jobId, out CancellationTokenSource? cts))
                cts.Cancel();
        }
    }

    public void CancelAll()
    {
        lock (_gate)
        {
            foreach (CancellationTokenSource cts in _jobs.Values)
                cts.Cancel();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (CancellationTokenSource cts in _jobs.Values)
            {
                cts.Cancel();
                cts.Dispose();
            }
            _jobs.Clear();
        }
    }
}
