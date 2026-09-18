namespace PwAssistant.Core.Models;

/// <summary>
/// Pure data describing one input: a key press or a UI click.
/// Delay/Repeat semantics are owned by <c>MacroExecutor</c>.
/// </summary>
public sealed class GameAction
{
    public ActionType Type { get; set; }

    /// <summary>Key name when <see cref="Type"/> is <see cref="ActionType.Key"/> (e.g. "F1", "SPACE").</summary>
    public string? Key { get; set; }

    /// <summary>Client-area fraction when <see cref="Type"/> is <see cref="ActionType.Click"/>.</summary>
    public RelativePosition? RelativePosition { get; set; }

    /// <summary>Button for Click actions. Defaults to Left (v1 recipe).</summary>
    public MouseButton Button { get; set; } = MouseButton.Left;

    private int _delayBeforeMs;
    public int DelayBeforeMs
    {
        get => _delayBeforeMs;
        set => _delayBeforeMs = Math.Max(0, value);
    }

    public RepeatSettings? Repeat { get; set; }

    /// <summary>
    /// Edit-time validation. A click without position is rejected here —
    /// never at dispatch time (business rule).
    /// </summary>
    public IEnumerable<string> Validate()
    {
        if (Type == ActionType.Key && string.IsNullOrWhiteSpace(Key))
            yield return "Key action requires a key name.";

        if (Type == ActionType.Click)
        {
            if (RelativePosition is null)
                yield return "Click action requires a relative position.";
            else if (!RelativePosition.IsValid)
                yield return "Click position must be within 0.0-1.0 on both axes.";
        }
    }
}
