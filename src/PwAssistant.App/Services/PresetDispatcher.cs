using PwAssistant.Core.Execution;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;

namespace PwAssistant.App.Services;

/// <summary>
/// Preset firing pipeline: optional countdown → dispatch → per-account log.
/// Countdown defaults to zero (the early 3s test delay is gone).
/// Focused batches (when the strategy opts in) restore the previous
/// foreground at the end, even under cancellation.
/// </summary>
public sealed class PresetDispatcher
{
    private readonly MacroJobRunner _runner;
    private readonly IInputStrategy _strategy;

    public PresetDispatcher(MacroJobRunner runner, IInputStrategy strategy)
    {
        _runner = runner;
        _strategy = strategy;
    }

    public async Task<PresetExecutionResult> FireAsync(
        Preset preset,
        int countdownSeconds = 0,
        IProgress<int>? countdown = null,
        IProgress<PresetProgress>? fireProgress = null,
        CancellationToken cancellationToken = default)
    {
        await Countdown.RunAsync(countdownSeconds, countdown, cancellationToken).ConfigureAwait(false);
        if (_strategy is not FocusedInputStrategy focused)
            return await _runner.ExecuteAsync(preset, fireProgress, cancellationToken).ConfigureAwait(false);
        focused.BeginBatch();
        try
        {
            return await _runner.ExecuteAsync(preset, fireProgress, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            await focused.EndBatchAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public void CancelAll() => _runner.CancelAll();
}
