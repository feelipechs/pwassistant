using System.Drawing;

namespace PwAssistant.Core.Tests;

/// <summary>
/// WCAG contrast guard for the dark palette. Hexes mirror
/// Themes/Colors.Dark.xaml by hand (tests run from %TEMP% outputs and
/// cannot reach repo XAML): keep in sync when the palette changes.
/// </summary>
public sealed class ThemeContrastTests
{
    private static readonly Dictionary<string, Color> Palette = new()
    {
        ["Background"] = Hex("0A0A0A"),
        ["Card"] = Hex("171717"),
        ["Raised"] = Hex("262626"),
        ["RaisedHover"] = Hex("303030"),
        ["Foreground"] = Hex("FAFAFA"),
        ["Primary"] = Hex("FAFAFA"),
        ["PrimaryHover"] = Hex("E5E5E5"),
        ["PrimaryForeground"] = Hex("171717"),
        ["MutedForeground"] = Hex("A3A3A3"),
        ["Input"] = Hex("737373"),
        ["Border"] = Hex("262626"),
        ["Ring"] = Hex("D4D4D4"),
        ["Destructive"] = Hex("F87171"),
        ["DestructiveSolid"] = Hex("C62828"),
        ["OnDestructiveSolid"] = Hex("FFFFFF"),
        ["Success"] = Hex("4ADE80"),
        ["Warning"] = Hex("FBBF24"),
    };

    private static Color Hex(string hex) => ColorTranslator.FromHtml("#" + hex);

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
    [MemberData(nameof(TextPairs))]
    public void TextContrast_MeetsAA(string foreground, string background)
    {
        Assert.True(
            Contrast(Palette[foreground], Palette[background]) >= 4.5,
            $"{foreground} on {background} below WCAG AA 4.5");
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
    public void ControlContrast_MeetsMinimum(string foreground, string background)
    {
        Assert.True(
            Contrast(Palette[foreground], Palette[background]) >= 3.0,
            $"{foreground} on {background} below 3.0");
    }
}
