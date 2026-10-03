using System.Drawing;
using PwAssistant.Avalonia.Themes;

namespace PwAssistant.Avalonia.Tests;

/// <summary>
/// WCAG contrast guard for the Avalonia shadcn tokens. Hexes come from
/// ShadcnPalette (single source; ShadcnTokens.axaml mirrors it by hand):
/// Dark keeps the WPF values, Light is the verified inversion, accents must
/// keep the Primary triple at AA in both variants.
/// </summary>
public sealed class ShadcnThemeTests
{
    private static Color Hex(string hex) => ColorTranslator.FromHtml(hex);

    private static double Contrast(Color a, Color b)
    {
        static double Channel(byte v)
        {
            double s = v / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
        static double Lum(Color c) =>
            0.2126 * Channel(c.R) + 0.7152 * Channel(c.G) + 0.0722 * Channel(c.B);
        double x = Lum(a), y = Lum(b);
        if (x < y) (x, y) = (y, x);
        return (x + 0.05) / (y + 0.05);
    }

    private static double Ratio(IReadOnlyDictionary<string, string> palette, string fg, string bg) =>
        Contrast(Hex(palette[fg]), Hex(palette[bg]));

    public static IEnumerable<object[]> Variants => new List<object[]>
    {
        new object[] { "Dark" },
        new object[] { "Light" },
    };

    private static IReadOnlyDictionary<string, string> Palette(string variant) =>
        variant == "Light" ? ShadcnPalette.Light : ShadcnPalette.Dark;

    public static IEnumerable<object[]> TextPairs => new List<object[]>
    {
        new object[] { "Foreground", "Background" },
        new object[] { "Foreground", "Card" },
        new object[] { "Foreground", "Raised" },
        new object[] { "Foreground", "RaisedHover" },
        new object[] { "MutedForeground", "Card" },
        new object[] { "MutedForeground", "Raised" },
        new object[] { "MutedForeground", "RaisedHover" },
        new object[] { "PrimaryForeground", "Primary" },
        new object[] { "PrimaryForeground", "PrimaryHover" },
        new object[] { "OnDestructiveSolid", "DestructiveSolid" },
        new object[] { "Destructive", "Card" },
        new object[] { "Success", "Card" },
        new object[] { "Warning", "Card" },
    };

    [Theory]
    [MemberData(nameof(Variants))]
    public void Tokens_HaveFullPalette(string variant)
    {
        var palette = Palette(variant);
        foreach (string key in new[]
            {
                "Background", "Card", "Raised", "RaisedHover", "Foreground",
                "Primary", "PrimaryHover", "PrimaryForeground", "MutedForeground",
                "Input", "Border", "Ring", "Destructive", "DestructiveSolid",
                "OnDestructiveSolid", "Success", "Warning",
            })
            Assert.True(palette.ContainsKey(key), $"{variant} missing {key}");
    }

    [Theory]
    [MemberData(nameof(TextPairs))]
    public void TextContrast_MeetsAA_InBothVariants(string foreground, string background)
    {
        Assert.True(Ratio(ShadcnPalette.Dark, foreground, background) >= 4.5,
            $"Dark {foreground} on {background} below WCAG AA 4.5");
        Assert.True(Ratio(ShadcnPalette.Light, foreground, background) >= 4.5,
            $"Light {foreground} on {background} below WCAG AA 4.5");
    }

    public static IEnumerable<object[]> ControlPairs => new List<object[]>
    {
        new object[] { "Input", "Background" },
        new object[] { "Input", "Card" },
        new object[] { "Input", "Raised" },
        new object[] { "Ring", "Card" },
        new object[] { "Ring", "Background" },
    };

    [Theory]
    [MemberData(nameof(ControlPairs))]
    public void ControlContrast_MeetsMinimum_InBothVariants(string foreground, string background)
    {
        Assert.True(Ratio(ShadcnPalette.Dark, foreground, background) >= 3.0,
            $"Dark {foreground} on {background} below 3.0");
        Assert.True(Ratio(ShadcnPalette.Light, foreground, background) >= 3.0,
            $"Light {foreground} on {background} below 3.0");
    }

    [Theory]
    [MemberData(nameof(Variants))]
    public void Accents_KeepPrimaryTripleAA(string variant)
    {
        var palette = Palette(variant);
        foreach (var (name, perVariant) in ShadcnPalette.Accents)
        {
            if (!perVariant.TryGetValue(variant, out var triple))
                continue; // Neutral = themed default, already covered above.

            double onPrimary = Contrast(Hex(triple["PrimaryForeground"]), Hex(triple["Primary"]));
            double onHover = Contrast(Hex(triple["PrimaryForeground"]), Hex(triple["PrimaryHover"]));
            Assert.True(onPrimary >= 4.5, $"{variant}/{name} Primary below AA 4.5");
            Assert.True(onHover >= 4.5, $"{variant}/{name} PrimaryHover below AA 4.5");

            // Accent Primary must stay visible against both surfaces.
            Assert.True(Contrast(Hex(triple["Primary"]), Hex(palette["Card"])) >= 3.0,
                $"{variant}/{name} Primary on Card below 3.0");
        }
    }
}
