using PwAssistant.Core.Launcher;
using PwAssistant.Core.Models;
using PwAssistant.Core.Sync;
using PwAssistant.Core.Ux;

namespace PwAssistant.Core.Tests;

public sealed class SyncServiceTests
{
    [Fact]
    public void DisabledService_CapturesNothing()
    {
        var sync = new SyncService();
        Assert.Null(sync.CaptureMasterClick(100, 100, 800, 600, isLeftButton: true));
    }

    [Fact]
    public void NonLeftButton_CapturesNothing()
    {
        var sync = new SyncService();
        sync.SetEnabled(true);
        Assert.Null(sync.CaptureMasterClick(100, 100, 800, 600, isLeftButton: false));
    }

    [Fact]
    public void ClickOutsideClientArea_CapturesNothing()
    {
        var sync = new SyncService();
        sync.SetEnabled(true);
        Assert.Null(sync.CaptureMasterClick(900, 100, 800, 600, isLeftButton: true));
    }

    [Fact]
    public void ValidClick_ReturnsFraction_AndReplicaConvertsBack()
    {
        var sync = new SyncService();
        sync.SetEnabled(true);

        RelativePosition? fraction = sync.CaptureMasterClick(200, 150, 800, 600, isLeftButton: true);
        Assert.NotNull(fraction);

        (int x, int y) = SyncService.ToReplicaPixels(fraction, 1024, 768);
        Assert.Equal(256, x);
        Assert.Equal(192, y);
    }

    [Fact]
    public void SyncedAccounts_AreTracked()
    {
        var sync = new SyncService();
        Guid id = Guid.NewGuid();
        sync.SetSyncedAccounts(new[] { id });
        Assert.True(sync.IsSynced(id));
        Assert.False(sync.IsSynced(Guid.NewGuid()));
    }
}

public sealed class GameLauncherTests
{
    [Fact]
    public void BuildStartInfo_UsesStartByPatcherArguments()
    {
        var server = new Server { ElementClientPath = @"C:\pw\elementclient.exe" };
        var account = new Account { Login = "hero", Role = "HeroNick" };

        var info = GameLauncher.BuildStartInfo(server, account, "pw-secret");

        Assert.Equal(@"C:\pw\elementclient.exe", info.FileName);
        Assert.Contains("startbypatcher", info.Arguments);
        Assert.Contains("user:hero", info.Arguments);
        Assert.Contains("role:HeroNick", info.Arguments);
    }

    [Fact]
    public void BuildStartInfo_AppendsForceServer_WhenSet()
    {
        var server = new Server { ElementClientPath = @"C:\pw\elementclient.exe", ForceServer = "forceserver:1" };
        var account = new Account { Login = "hero", Role = "HeroNick" };

        var info = GameLauncher.BuildStartInfo(server, account, "pw");

        Assert.Contains("forceserver:1", info.Arguments);
    }

    [Fact]
    public void BuildStartInfo_RejectsServerWithoutClientPath()
    {
        var server = new Server { ElementClientPath = string.Empty };
        Assert.Throws<ArgumentException>(() =>
            GameLauncher.BuildStartInfo(server, new Account(), "pw"));
    }

    [Fact]
    public void BuildStartInfo_SetsWorkingDirectoryToClientFolder()
    {
        string clientPath = Path.Combine("game", "x64", "elementclient_64.exe");
        var server = new Server { ElementClientPath = clientPath };

        var info = GameLauncher.BuildStartInfo(server, new Account { Login = "hero" }, "pw");

        Assert.Equal(Path.Combine("game", "x64"), info.WorkingDirectory);
    }
}

public sealed class CountdownTests
{
    [Fact]
    public async Task ZeroSeconds_CompletesImmediately()
    {
        var progress = new Progress<int>();
        await Countdown.RunAsync(0, progress);
    }

    [Fact]
    public async Task NegativeSeconds_TreatedAsZero()
    {
        await Countdown.RunAsync(-5);
    }
}
