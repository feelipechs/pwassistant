namespace PwAssistant.Core.Sync;

/// <summary>
/// Window-switching configuration (B4-config). CycleKey is a virtual-key
/// code from a curated safe set (no letters/digits that would hijack
/// typing). Persisted in AppData; the on/off mode stays session-only.
/// </summary>
public sealed class FocusSettings
{
    public const int DefaultCycleKey = 0xC0;

    public int CycleKey { get; set; } = DefaultCycleKey;
    public bool NumpadEnabled { get; set; } = true;
    public bool ShiftTapEnabled { get; set; } = true;
}
