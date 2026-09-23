using PwAssistant.Core.Execution;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;

namespace PwAssistant.App.Services;

/// <summary>
/// Preset loop (mini-mode toggle): re-fires a preset until toggled off.
/// Two states: armed (toggle marked, nothing runs yet) vs firing (loop
/// active). Clicking a preset starts firing when armed, stops firing
/// when running — the toggle never unmarks on click. Countdown only on
/// start; fixed 1 s gap between iterations. Offline accounts keep being
/// skipped by the executor; closing the mini window stops every loop
/// via <see cref="StopAll"/> (armed survives: reopening shows marked).
/// </summary>
public sealed class LoopController : IDisposable
{
    private static readonly TimeSpan IterationGap = TimeSpan.FromSeconds(1);

    private readonly PresetDispatcher _dispatcher;
    private readonly FileLogger _log;
    private readonly Dictionary<Guid, CancellationTokenSource> _loops = new();
    private readonly HashSet<Guid> _armed = new();
    private readonly object _gate = new();
    private bool _disposed;

    public event Action? Changed;

    /// <summary>Raised on the loop thread after every iteration.</summary>
    public event Action<Guid, PresetExecutionResult>? Progressed;

    public LoopController(PresetDispatcher dispatcher, FileLogger log)
    {
        _dispatcher = dispatcher;
        _log = log;
    }

    /// <summary>True while the loop is firing (blink).</summary>
    public bool IsLooping(Guid presetId)
    {
        lock (_gate)
            return _loops.ContainsKey(presetId);
    }

    /// <summary>True while the toggle is marked (persists across stops).</summary>
    public bool IsArmed(Guid presetId)
    {
        lock (_gate)
            return _armed.Contains(presetId);
    }

    public void SetArmed(Preset preset, bool armed)
    {
        ArgumentNullException.ThrowIfNull(preset);
        lock (_gate)
        {
            if (armed)
                _armed.Add(preset.Id);
            else
                _armed.Remove(preset.Id);
        }
        Changed?.Invoke();
    }

    /// <summary>Starts firing an armed preset (idempotent).</summary>
    public void Start(Preset preset)
    {
        ArgumentNullException.ThrowIfNull(preset);
        lock (_gate)
        {
            if (_loops.ContainsKey(preset.Id))
                return;
            ObjectDisposedException.ThrowIf(_disposed, this);
            _loops[preset.Id] = new CancellationTokenSource();
        }
        _ = RunLoopAsync(preset);
        Changed?.Invoke();
    }

    /// <summary>Stops firing but keeps the toggle marked.</summary>
    public void Stop(Guid presetId)
    {
        lock (_gate)
        {
            if (_loops.TryGetValue(presetId, out CancellationTokenSource? cts))
                cts.Cancel();
        }
    }

    /// <summary>Stops firing and unmarks (entity deleted).</summary>
    public void Forget(Guid presetId)
    {
        lock (_gate)
        {
            _armed.Remove(presetId);
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
            while (!cts.Token.IsCancellationRequested)
            {
                PresetExecutionResult result = await _dispatcher
                    .FireAsync(preset, 0, null, null, cts.Token).ConfigureAwait(false);
                Progressed?.Invoke(preset.Id, result);
                await Task.Delay(IterationGap, cts.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Toggle off or StopAll: normal exit.
        }
        catch (Exception ex)
        {
            // Firing errors stop the loop but keep the toggle marked —
            // clicking the preset restarts. Never an unobserved exception.
            _log.Error($"Loop {preset.Name} failed: {ex.Message}");
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
