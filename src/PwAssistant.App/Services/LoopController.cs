using PwAssistant.Core.Models;

namespace PwAssistant.App.Services;

/// <summary>
/// Preset loop (mini-mode toggle): re-fires a preset until toggled off.
/// Countdown only on start; fixed 1 s gap between iterations. Offline
/// accounts keep being skipped by the executor; closing the mini window
/// stops every loop via <see cref="StopAll"/>.
/// </summary>
public sealed class LoopController : IDisposable
{
    private static readonly TimeSpan IterationGap = TimeSpan.FromSeconds(1);

    private readonly PresetDispatcher _dispatcher;
    private readonly Dictionary<Guid, CancellationTokenSource> _loops = new();
    private readonly object _gate = new();
    private bool _disposed;

    public event Action? Changed;

    public LoopController(PresetDispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    public bool IsLooping(Guid presetId)
    {
        lock (_gate)
            return _loops.ContainsKey(presetId);
    }

    public void ToggleLoop(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        lock (_gate)
        {
            if (_loops.TryGetValue(preset.Id, out CancellationTokenSource? existing))
            {
                existing.Cancel();
                return;
            }
            ObjectDisposedException.ThrowIf(_disposed, this);
            _loops[preset.Id] = new CancellationTokenSource();
        }
        _ = RunLoopAsync(preset);
        Changed?.Invoke();
    }

    public void Stop(Guid presetId)
    {
        lock (_gate)
        {
            if (_loops.TryGetValue(presetId, out CancellationTokenSource? cts))
                cts.Cancel();
        }
    }

    public void StopAll()
    {
        lock (_gate)
        {
            foreach (CancellationTokenSource cts in _loops.Values)
                cts.Cancel();
        }
    }

    private async Task RunLoopAsync(Preset preset)
    {
        CancellationTokenSource cts;
        lock (_gate)
        {
            if (!_loops.TryGetValue(preset.Id, out cts!))
                return;
        }

        try
        {
            bool first = true;
            while (!cts.Token.IsCancellationRequested)
            {
                await _dispatcher.FireAsync(preset, first ? 3 : 0, null, null, cts.Token).ConfigureAwait(false);
                first = false;
                await Task.Delay(IterationGap, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Toggle off or StopAll: normal exit.
        }
        finally
        {
            lock (_gate)
            {
                _loops.Remove(preset.Id, out _);
            }
            cts.Dispose();
            Changed?.Invoke();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (CancellationTokenSource cts in _loops.Values)
                cts.Cancel();
            _loops.Clear();
        }
        GC.SuppressFinalize(this);
    }
}
