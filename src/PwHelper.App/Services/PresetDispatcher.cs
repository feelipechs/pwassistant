using PwHelper.Core.Execution;
using PwHelper.Core.Models;
using PwHelper.Core.Ux;

namespace PwHelper.App.Services;

/// <summary>
/// Preset firing pipeline (M6): countdown → dispatch → per-account log.
/// Countdown first so the user looks away from the game before anything fires.
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
        int countdownSeconds = 3,
        IProgress<int>? countdown = null,
        CancellationToken cancellationToken = default)
    {
        await Countdown.RunAsync(countdownSeconds, countdown, cancellationToken).ConfigureAwait(false);
        return await _runner.ExecuteAsync(preset, cancellationToken).ConfigureAwait(false);
    }

    public void CancelAll() => _runner.CancelAll();
}
