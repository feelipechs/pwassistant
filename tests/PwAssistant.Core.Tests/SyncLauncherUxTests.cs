using PwAssistant.Core.Input;
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
    public void ShortcutDefinition_IsUniquePerAccount_AndMirrorsArguments()
    {
        var server = new Server { ElementClientPath = @"C:\pw\elementclient.exe" };
        var first = new Account { Login = "a", Role = "A" };
        var second = new Account { Login = "b", Role = "B" };

        ShortcutDefinition one = GameLauncher.BuildShortcutDefinition(
            server, first, "pw", @"C:\clients", @"C:\icons");
        ShortcutDefinition two = GameLauncher.BuildShortcutDefinition(
            server, second, "pw", @"C:\clients", @"C:\icons");

        Assert.EndsWith(".lnk", one.ShortcutPath);
        Assert.NotEqual(one.ShortcutPath, two.ShortcutPath);
        Assert.Equal(
            GameLauncher.BuildStartInfo(server, first, "pw").Arguments,
            one.Arguments);
    }

    [Fact]
    public void ShortcutDefinition_UsesClassIcon_WhenKnown()
    {
        var server = new Server { ElementClientPath = @"C:\pw\elementclient.exe" };
        var account = new Account { Login = "a", Role = "A", Class = "barbaro" };

        ShortcutDefinition definition = GameLauncher.BuildShortcutDefinition(
            server, account, "pw", @"C:\clients", @"C:\icons");

        Assert.Equal(@"C:\icons\barbaro.ico", definition.IconLocation);
    }

    [Fact]
    public void IdentityShortcut_HasNoSecrets_ButSamePath()
    {
        var server = new Server { ElementClientPath = @"C:\pw\elementclient.exe" };
        var account = new Account { Login = "hero", Role = "HeroNick" };

        ShortcutDefinition identity = GameLauncher.BuildIdentityShortcutDefinition(
            server, account, @"C:\clients", @"C:\icons");
        ShortcutDefinition full = GameLauncher.BuildShortcutDefinition(
            server, account, "pw", @"C:\clients", @"C:\icons");

        Assert.Equal(full.ShortcutPath, identity.ShortcutPath);
        Assert.Equal(string.Empty, identity.Arguments);
        Assert.DoesNotContain("pwd:", identity.Arguments);
        Assert.DoesNotContain("user:", identity.Arguments);
    }

    [Fact]
    public async Task LaunchAsync_ScrubsShortcut_EvenWhenLaunchFails()
    {
        var server = new Server { ElementClientPath = @"C:\definitely\missing\elementclient.exe" };
        var account = new Account { Login = "hero", Role = "HeroNick" };
        var ensured = new List<ShortcutDefinition>();
        var launcher = new GameLauncher(new NeverAliveResolver(), ensured.Add);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            launcher.LaunchAsync(server, account, "pw-secret", TimeSpan.FromSeconds(1)));

        Assert.Equal(2, ensured.Count);
        Assert.Contains("pwd:pw-secret", ensured[0].Arguments);
        Assert.Equal(ensured[0].ShortcutPath, ensured[1].ShortcutPath);
        Assert.Equal(string.Empty, ensured[1].Arguments);
    }

    [Fact]
    public void CleanseShortcuts_RewritesKnown_AndDeletesOrphans()
    {
        string dir = Path.Combine(Path.GetTempPath(), $"ditto-clients-{Guid.NewGuid():N}");
        Directory.CreateDirectory(dir);
        try
        {
            var account = new Account { Login = "hero", Role = "HeroNick" };
            var server = new Server { ElementClientPath = @"C:\pw\elementclient.exe" };
            server.Accounts.Add(account);
            string orphan = Path.Combine(dir, $"{Guid.NewGuid():N}.lnk");
            File.WriteAllText(orphan, "stale credentials");
            string foreign = Path.Combine(dir, "notes.txt");
            File.WriteAllText(foreign, "keep me");
            var ensured = new List<ShortcutDefinition>();

            int fixedCount = GameLauncher.CleanseShortcuts(
                new[] { server }, ensured.Add, dir, @"C:\icons");

            Assert.Equal(2, fixedCount);
            ShortcutDefinition rewritten = Assert.Single(ensured);
            Assert.Equal(string.Empty, rewritten.Arguments);
            Assert.False(File.Exists(orphan));
            Assert.True(File.Exists(foreign));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    private sealed class NeverAliveResolver : IWindowResolver
    {
        public IntPtr ResolveWindow(int processId) => IntPtr.Zero;
        public bool IsWindowAlive(IntPtr windowHandle) => false;
        public (int Width, int Height) GetClientSize(IntPtr windowHandle) => (0, 0);
        public (int X, int Y) ScreenToClientPoint(IntPtr windowHandle, int screenX, int screenY) => (0, 0);
        public IntPtr ResolveTopWindowAtPoint(int screenX, int screenY) => IntPtr.Zero;
        public IntPtr GetForegroundWindow() => IntPtr.Zero;
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
