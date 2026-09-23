namespace PwAssistant.Core.Execution;

/// <summary>
/// Key-name → virtual-key resolution (pure data, no WinAPI).
/// Mirrors the proven PowerShell battery mapping.
/// </summary>
public static class KeyCodes
{
    private static readonly Dictionary<string, int> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SPACE"] = 0x20,
        ["ENTER"] = 0x0D,
        ["ESC"] = 0x1B,
        ["ESCAPE"] = 0x1B,
        ["TAB"] = 0x09,
        ["F1"] = 0x70,
        ["F2"] = 0x71,
        ["F3"] = 0x72,
        ["F4"] = 0x73,
        ["F5"] = 0x74,
        ["F6"] = 0x75,
        ["F7"] = 0x76,
        ["F8"] = 0x77,
        ["F9"] = 0x78,
        ["F10"] = 0x79,
        ["F11"] = 0x7A,
        ["F12"] = 0x7B
    };

    /// <summary>Curated key names offered by the preset editor (no free text).
    /// Required set: F1–F12, 0–9, A–Z, TAB, SPACE. ENTER/ESC still resolve
    /// (grandfathered saved data) but are no longer offered.</summary>
    public static IReadOnlyList<string> PresetKeys { get; } =
    [
        "F1", "F2", "F3", "F4", "F5", "F6", "F7", "F8", "F9", "F10", "F11", "F12",
        "0", "1", "2", "3", "4", "5", "6", "7", "8", "9",
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        "TAB", "SPACE",
    ];

    public static int Resolve(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string normalized = name.Trim();

        if (Names.TryGetValue(normalized, out int vk))
            return vk;
        if (normalized.Length == 1)
            return char.ToUpperInvariant(normalized[0]);
        if (normalized.StartsWith("0X", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(normalized[2..], System.Globalization.NumberStyles.HexNumber, null, out int hex))
            return hex;
        if (int.TryParse(normalized, out int numeric))
            return numeric;

        throw new ArgumentException($"Unknown key name: {name}", nameof(name));
    }
}
