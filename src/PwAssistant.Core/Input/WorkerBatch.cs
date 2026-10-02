using PwAssistant.Core.Models;

namespace PwAssistant.Core.Input;

/// <summary>
/// Ordered per-account action buffer for the worker path. Dumb list:
/// ordering (keys, clicks, sleeps interleaved exactly as produced) is
/// the whole contract — timing equivalence with in-process dispatch.
/// One instance per account per batch; flushed once via
/// <see cref="SenderRunner"/>.
/// </summary>
public sealed class WorkerBatch
{
    public int ProcessId { get; }

    private readonly List<SenderAction> _actions = new();

    public WorkerBatch(int processId)
    {
        if (processId <= 0)
            throw new ArgumentOutOfRangeException(nameof(processId));
        ProcessId = processId;
    }

    public IReadOnlyList<SenderAction> Actions => _actions;

    public bool IsEmpty => _actions.Count == 0;

    public void AddKey(int virtualKey) => _actions.Add(SenderAction.Key(virtualKey));

    public void AddClick(double relativeX, double relativeY, MouseButton button) =>
        _actions.Add(SenderAction.Click(relativeX, relativeY, button switch
        {
            MouseButton.Right => "right",
            _ => "left",
        }));

    public void AddSleep(int millisecondsDelay)
    {
        if (millisecondsDelay > 0)
            _actions.Add(SenderAction.Sleep(millisecondsDelay));
    }

    public SenderWorkload ToWorkload(bool verbose) => new()
    {
        ProcessId = ProcessId,
        Verbose = verbose,
        Actions = new List<SenderAction>(_actions),
    };
}
