namespace PwAssistant.Avalonia.Themes;

/// <summary>
/// Single source of the shadcn hexes. Themes/ShadcnTokens.axaml mirrors
/// these by hand (same convention as WPF Colors.Dark.xaml + ThemeContrastTests):
/// keep in sync when the palette changes. Accents override only the Primary
/// triple; Ring stays themed so control contrast never regresses.
/// </summary>
public static class ShadcnPalette
{
    public static readonly IReadOnlyDictionary<string, string> Dark = new Dictionary<string, string>
    {
        ["Background"] = "#0A0A0A",
        ["Card"] = "#171717",
        ["Raised"] = "#262626",
        ["RaisedHover"] = "#303030",
        ["Foreground"] = "#FAFAFA",
        ["Primary"] = "#FAFAFA",
        ["PrimaryHover"] = "#E5E5E5",
        ["PrimaryForeground"] = "#171717",
        ["MutedForeground"] = "#A3A3A3",
        ["Input"] = "#737373",
        ["Border"] = "#262626",
        ["Ring"] = "#D4D4D4",
        ["Destructive"] = "#F87171",
        ["DestructiveSolid"] = "#C62828",
        ["OnDestructiveSolid"] = "#FFFFFF",
        ["Success"] = "#4ADE80",
        ["Warning"] = "#FBBF24",
    };

    public static readonly IReadOnlyDictionary<string, string> Light = new Dictionary<string, string>
    {
        ["Background"] = "#FAFAFA",
        ["Card"] = "#FFFFFF",
        ["Raised"] = "#F4F4F5",
        ["RaisedHover"] = "#E4E4E7",
        ["Foreground"] = "#0A0A0A",
        ["Primary"] = "#18181B",
        ["PrimaryHover"] = "#27272A",
        ["PrimaryForeground"] = "#FAFAFA",
        ["MutedForeground"] = "#52525B",
        ["Input"] = "#737373",
        ["Border"] = "#E4E4E7",
        ["Ring"] = "#18181B",
        ["Destructive"] = "#DC2626",
        ["DestructiveSolid"] = "#B91C1C",
        ["OnDestructiveSolid"] = "#FFFFFF",
        ["Success"] = "#15803D",
        ["Warning"] = "#B45309",
    };

    /// <summary>
    /// Accent name → variant ("Dark"/"Light") → Primary triple.
    /// Neutral = themed default (no override). Triples differ per variant so
    /// the Primary button keeps WCAG AA and stays visible on the Card surface
    /// in both modes (light surfaces need darker accents).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>> Accents =
        new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>>>
        {
            ["Neutral"] = new Dictionary<string, IReadOnlyDictionary<string, string>>(),
            ["Gold"] = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["Dark"] = new Dictionary<string, string>
                {
                    ["Primary"] = "#C9A227",
                    ["PrimaryHover"] = "#B8941F",
                    ["PrimaryForeground"] = "#171717",
                },
                ["Light"] = new Dictionary<string, string>
                {
                    ["Primary"] = "#A16207",
                    ["PrimaryHover"] = "#854D0E",
                    ["PrimaryForeground"] = "#FFFFFF",
                },
            },
            ["Blue"] = new Dictionary<string, IReadOnlyDictionary<string, string>>
            {
                ["Dark"] = new Dictionary<string, string>
                {
                    ["Primary"] = "#2563EB",
                    ["PrimaryHover"] = "#1D4ED8",
                    ["PrimaryForeground"] = "#FFFFFF",
                },
                ["Light"] = new Dictionary<string, string>
                {
                    ["Primary"] = "#2563EB",
                    ["PrimaryHover"] = "#1D4ED8",
                    ["PrimaryForeground"] = "#FFFFFF",
                },
            },
        };
}
