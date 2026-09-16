using PwHelper.Core.Input;
using PwHelper.Core.Models;

namespace PwHelper.Core.Execution;

public sealed record AccountExecutionResult(
    Guid AccountId,
    bool Skipped,
    string? Reason = null,
    string? Error = null);

public sealed record PresetExecutionResult(
    Guid JobId,
    Guid PresetId,
    bool Canceled,
    IReadOnlyList<AccountExecutionResult> Accounts);

/// <summary>
/// Dispatches a preset to live game windows. Offline accounts are skipped
/// with a log entry — the batch never aborts. DOWN/UP pairs inside the
/// strategy are atomic; cancellation applies between actions.
/// </summary>
public sealed class MacroExecutor
{
    private readonly IInputStrategy _strategy;
    private readonly Func<Guid, IWindowTarget?> _targetResolver;

    public MacroExecutor(IInputStrategy strategy, Func<Guid, IWindowTarget?> targetResolver)
    {
        _strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
        _targetResolver = targetResolver ?? throw new ArgumentNullException(nameof(targetResolver));
    }

    public async Task<PresetExecutionResult> ExecuteAsync(
        Preset preset, Guid jobId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preset);

        if (preset.ExecutionMode == ExecutionMode.Simultaneous)
            return await ExecuteSimultaneousAsync(preset, jobId, cancellationToken).ConfigureAwait(false);

        var results = new List<AccountExecutionResult>();
        foreach (AccountAction accountAction in preset.Actions)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results.Add(await ExecuteSingleAsync(accountAction, cancellationToken).ConfigureAwait(false));
        }
        return new PresetExecutionResult(jobId, preset.Id, false, results);
    }

    private async Task<PresetExecutionResult> ExecuteSimultaneousAsync(
        Preset preset, Guid jobId, CancellationToken cancellationToken)
    {
        Task<AccountExecutionResult>[] tasks = preset.Actions
            .Select(accountAction => ExecuteSingleAsync(accountAction, cancellationToken))
            .ToArray();

        AccountExecutionResult[] results;
        try
        {
            results = await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }

        return new PresetExecutionResult(jobId, preset.Id, false, results);
    }

    private async Task<AccountExecutionResult> ExecuteSingleAsync(
        AccountAction accountAction, CancellationToken cancellationToken)
    {
        IWindowTarget? target = _targetResolver(accountAction.AccountId);
        if (target is null || target.WindowHandle == IntPtr.Zero)
            return new AccountExecutionResult(accountAction.AccountId, Skipped: true, Reason: "offline");

        foreach (string error in accountAction.Action.Validate())
            return new AccountExecutionResult(accountAction.AccountId, Skipped: true, Reason: error);

        try
        {
            if (accountAction.Action.DelayBeforeMs > 0)
                await Task.Delay(accountAction.Action.DelayBeforeMs, cancellationToken).ConfigureAwait(false);

            int times = accountAction.Action.Repeat?.NormalizedTimes ?? 1;
            int interval = accountAction.Action.Repeat?.NormalizedIntervalMs ?? 0;

            for (int i = 0; i < times; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (i > 0 && interval > 0)
                    await Task.Delay(interval, cancellationToken).ConfigureAwait(false);

                await SendOnceAsync(target, accountAction.Action, cancellationToken).ConfigureAwait(false);
            }

            return new AccountExecutionResult(accountAction.AccountId, Skipped: false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new AccountExecutionResult(accountAction.AccountId, Skipped: true, Error: ex.Message);
        }
    }

    private Task SendOnceAsync(IWindowTarget target, GameAction action, CancellationToken cancellationToken)
    {
        if (action.Type == ActionType.Click && action.RelativePosition is not null)
            return _strategy.SendUiClickAsync(
                target, action.RelativePosition.X, action.RelativePosition.Y, cancellationToken);

        return _strategy.SendKeyAsync(target, ResolveKey(action.Key), cancellationToken);
    }

    private static int ResolveKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("Key action requires a key name.");
        return KeyCodes.Resolve(key);
    }
}
