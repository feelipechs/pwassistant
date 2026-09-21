using PwAssistant.Core.Execution;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;

namespace PwAssistant.App.Services;

/// <summary>
/// Preset firing pipeline: optional countdown → dispatch → per-account log.
/// Countdown defaults to zero (the early 3s test delay is gone).
/// </summary>
public sealed class PresetDispatcher
{
    private readonly MacroJobRunner _runner;

    public PresetDispatcher(MacroJobRunner runner)
    {
        _runner = runner;
    }

    public async Task<PresetExecutionResult> FireAsync(
        Preset preset,
        int countdownSeconds = 0,
        IProgress<int>? countdown = null,
        IProgress<PresetProgress>? fireProgress = null,
        CancellationToken cancellationToken = default)
    {
        await Countdown.RunAsync(countdownSeconds, countdown, cancellationToken).ConfigureAwait(false);
        return await _runner.ExecuteAsync(preset, fireProgress, cancellationToken).ConfigureAwait(false);
    }

    public void CancelAll() => _runner.CancelAll();
}
