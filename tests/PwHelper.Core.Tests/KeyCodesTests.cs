using PwHelper.Core.Execution;

namespace PwHelper.Core.Tests;

public sealed class KeyCodesTests
{
    [Fact]
    public void PresetKeys_AllResolve()
    {
        Assert.NotEmpty(KeyCodes.PresetKeys);
        foreach (string name in KeyCodes.PresetKeys)
            Assert.True(KeyCodes.Resolve(name) != 0, name);
    }
}
