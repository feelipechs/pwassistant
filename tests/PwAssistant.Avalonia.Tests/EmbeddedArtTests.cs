using System.Text;
using PwAssistant.Core.Models;

namespace PwAssistant.Avalonia.Tests;

/// <summary>
/// Proves every art asset ships inside the assembly. Avalonia packs all
/// AvaloniaResource items into the single `!AvaloniaResources` manifest
/// entry, so manifest names alone prove nothing — this scans the bundle
/// content for the file names (ClassImageConverter + WindowChrome open
/// the same bundle at runtime via AssetLoader).
/// </summary>
public sealed class EmbeddedArtTests
{
    private static string BundleText()
    {
        using Stream? stream = typeof(App).Assembly.GetManifestResourceStream("!AvaloniaResources");
        Assert.NotNull(stream);
        return new StreamReader(stream, Encoding.Latin1).ReadToEnd();
    }

    [Fact]
    public void ClassPortraits_AreEmbedded()
    {
        string bundle = BundleText();
        foreach (ClassInfo info in ClassCatalog.All)
            Assert.Contains(info.ImageFile, bundle);
    }

    [Fact]
    public void AppArt_IsEmbedded()
    {
        string bundle = BundleText();
        Assert.Contains("AppIcon.png", bundle);
        Assert.Contains("App.ico", bundle);
    }
}
