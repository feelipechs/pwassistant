using PwHelper.Core.Execution;

namespace PwHelper.WinApi;

/// <summary>
/// Key name → virtual-key code. Thin alias over Core's table so WinApi
/// consumers (Probe, hotkeys) share the single mapping.
/// </summary>
public static class KeyMapper
{
    public static int Resolve(string name) => KeyCodes.Resolve(name);
}
