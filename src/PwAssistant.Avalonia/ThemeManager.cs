using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Styling;
using PwAssistant.Avalonia.Themes;

namespace PwAssistant.Avalonia;

/// <summary>
/// Runtime theme switch (Dark / Light / System) + accent override, persisted
/// to %AppData%/PwAssistant/avalonia-theme.json (no secrets, UI-only).
/// Accents override only the Primary triple at app level; clearing them
/// restores the themed default. Ring stays themed so contrast never regresses.
/// </summary>
public static class ThemeManager
{
    public static string Variant { get; private set; } = "Dark";
    public static string Accent { get; private set; } = "Neutral";

    public static void Apply(ThemeVariant variant) => ApplyVariant(variant);

    public static void ApplyVariant(ThemeVariant variant)
    {
        Variant = variant == ThemeVariant.Light ? "Light"
            : variant == ThemeVariant.Dark ? "Dark" : "System";

        if (Application.Current is App app)
            app.RequestedThemeVariant = variant;

        RefreshAccent();
        RefreshCaptions();
        Save();
    }

    public static void ApplyAccent(string accent)
    {
        Accent = ShadcnPalette.Accents.ContainsKey(accent) ? accent : "Neutral";
        RefreshAccent();
        Save();
    }

    /// <summary>Re-applies the accent triple matching the effective variant
    /// (matters when following the OS theme).</summary>
    public static void RefreshAccent()
    {
        if (Application.Current is not App app)
            return;

        foreach (string key in new[] { "Brush.Primary", "Brush.PrimaryHover", "Brush.PrimaryForeground" })
            app.Resources.Remove(key);

        string effective = app.ActualThemeVariant == ThemeVariant.Light ? "Light" : "Dark";
        if (ShadcnPalette.Accents.TryGetValue(Accent, out var perVariant)
            && perVariant.TryGetValue(effective, out var triple))
        {
            foreach (var (token, hex) in triple)
                app.Resources[token] = new SolidColorBrush(Color.Parse(hex));
        }
    }

    public static string Current() =>
        Application.Current?.ActualThemeVariant.ToString() ?? ThemeVariant.Dark.ToString();

    public static bool IsDark() =>
        (Application.Current?.ActualThemeVariant ?? ThemeVariant.Dark) != ThemeVariant.Light;

    /// <summary>Repaints native captions after a theme switch (Dark = dark caption).</summary>
    public static void RefreshCaptions()
    {
        try
        {
            if (Application.Current?.ApplicationLifetime
                is IClassicDesktopStyleApplicationLifetime desktop)
            {
                bool dark = IsDark();
                foreach (Window window in desktop.Windows)
                {
                    IntPtr handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                    WinApi.CaptionTheme.Apply(handle, dark);
                }
            }
        }
        catch
        {
            // Caption theming never breaks a theme switch.
        }
    }

    public static void Restore()
    {
        (string variant, string accent) = Load();

        Variant = variant;
        Accent = ShadcnPalette.Accents.ContainsKey(accent) ? accent : "Neutral";

        if (Application.Current is App app)
        {
            app.RequestedThemeVariant = Variant switch
            {
                "Light" => ThemeVariant.Light,
                "Dark" => ThemeVariant.Dark,
                _ => ThemeVariant.Default,
            };
        }

        RefreshAccent();
    }

    private static string SettingsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PwAssistant", "avalonia-theme.json");

    private static (string Variant, string Accent) Load()
    {
        try
        {
            string path = SettingsPath();
            if (!File.Exists(path))
                return ("Dark", "Neutral");

            using JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            JsonElement root = doc.RootElement;
            string variant = root.TryGetProperty("variant", out JsonElement v) ? v.GetString() ?? "Dark" : "Dark";
            string accent = root.TryGetProperty("accent", out JsonElement a) ? a.GetString() ?? "Neutral" : "Neutral";
            return (variant, accent);
        }
        catch
        {
            return ("Dark", "Neutral");
        }
    }

    private static void Save()
    {
        try
        {
            string path = SettingsPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(new { variant = Variant, accent = Accent }));
        }
        catch
        {
            // Theme persistence never breaks the app.
        }
    }
}
