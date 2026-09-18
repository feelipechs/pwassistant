using PwAssistant.Core.Models;

namespace PwAssistant.Core.Tests;

public sealed class ClassCatalogTests
{
    [Fact]
    public void AllClasses_Resolve_WithImageFile()
    {
        Assert.Equal(17, ClassCatalog.All.Count);
        foreach (ClassInfo info in ClassCatalog.All)
        {
            Assert.True(ClassCatalog.TryGet(info.Key, out ClassInfo resolved));
            Assert.Equal(info.Abbreviation, resolved.Abbreviation);
            Assert.Equal(info.Key + ".png", resolved.ImageFile);
        }
    }

    [Fact]
    public void UnknownOrEmpty_DoesNotResolve()
    {
        Assert.False(ClassCatalog.TryGet("xiii", out _));
        Assert.False(ClassCatalog.TryGet(null, out _));
        Assert.False(ClassCatalog.TryGet("  ", out _));
    }
}
