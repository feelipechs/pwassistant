namespace PwAssistant.Core.Models;

public enum ActionType
{
    Key,
    Click
}

public enum MouseButton
{
    Left,
    Right
}

/// <summary>Preset dispatch order. Sequential-only by design: synchronized
/// multi-client bursts trip server-side bot heuristics (observed account
/// kicks); PW Helper is sequential-only for the same reason.</summary>
public enum ExecutionMode
{
    Sequential
}

public enum AccountStatus
{
    Offline,
    Online
}
