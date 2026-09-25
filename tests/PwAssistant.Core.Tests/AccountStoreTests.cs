using System.Text.Json;
using PwAssistant.Core.Models;
using PwAssistant.Core.Storage;

namespace PwAssistant.Core.Tests;

internal sealed class TestProtector : ISecretProtector
{
    public string Protect(string plaintext) =>
        "TEST:" + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(plaintext));

    public string Reveal(string protectedPayload)
    {
        Assert.StartsWith("TEST:", protectedPayload);
        return System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(protectedPayload["TEST:".Length..]));
    }
}

public sealed class AccountStoreTests : IDisposable
{
    private readonly string _file = Path.Combine(Path.GetTempPath(), $"pwassistant-test-{Guid.NewGuid():N}.json");

    public void Dispose()
    {
        if (File.Exists(_file)) File.Delete(_file);
        if (File.Exists(_file + ".tmp")) File.Delete(_file + ".tmp");
    }

    private static (Server Server, Account Account) SampleAccount()
    {
        var server = new Server { Name = "TestServer", ElementClientPath = @"C:\game\elementclient.exe" };
        var account = new Account { ServerId = server.Id, Login = "hero", Role = "HeroNick" };
        server.Accounts.Add(account);
        return (server, account);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTripsProtectedPassword()
    {
        var store = new JsonFileAccountStore(new TestProtector());
        (Server server, Account account) = SampleAccount();
        store.SetPassword(account, "s3cr3t!");
        var data = new AppData { Servers = { server } };

        await store.SaveAsync(_file, data);
        AppData loaded = await store.LoadAsync(_file);

        Assert.Single(loaded.Servers);
        Assert.Equal("s3cr3t!", store.RevealPassword(loaded.Servers[0].Accounts[0]));
    }

    [Fact]
    public async Task SavedFile_NeverContainsPlaintextPassword()
    {
        var store = new JsonFileAccountStore(new TestProtector());
        (Server server, Account account) = SampleAccount();
        store.SetPassword(account, "s3cr3t!");
        await store.SaveAsync(_file, new AppData { Servers = { server } });

        string raw = await File.ReadAllTextAsync(_file);
        Assert.DoesNotContain("s3cr3t!", raw);
    }

    [Fact]
    public async Task RuntimeFields_AreNeverPersisted()
    {
        var store = new JsonFileAccountStore(new TestProtector());
        (Server server, Account account) = SampleAccount();
        store.SetPassword(account, "pw");
        account.ProcessId = 1234;
        account.WindowHandle = new IntPtr(5678);
        await store.SaveAsync(_file, new AppData { Servers = { server } });

        AppData loaded = await store.LoadAsync(_file);
        Account reloaded = loaded.Servers[0].Accounts[0];
        Assert.Null(reloaded.ProcessId);
        Assert.Equal(IntPtr.Zero, reloaded.WindowHandle);
        Assert.Equal(Models.AccountStatus.Offline, reloaded.Status);
    }

    [Fact]
    public async Task Status_IsOnline_WhenWindowHandleSet()
    {
        var account = new Account { WindowHandle = new IntPtr(1) };
        Assert.Equal(Models.AccountStatus.Online, account.Status);
        await Task.CompletedTask;
    }

    [Fact]
    public void JsonShape_RuntimeFields_AbsentFromSerializedPayload()
    {
        var account = new Account
        {
            Login = "hero",
            EncryptedPassword = "TEST:eA==",
            ProcessId = 42,
            WindowHandle = new IntPtr(7)
        };
        string raw = JsonSerializer.Serialize(account);
        Assert.DoesNotContain("ProcessId", raw);
        Assert.DoesNotContain("WindowHandle", raw);
        Assert.DoesNotContain("Status", raw);
    }

    [Fact]
    public async Task ConcurrentSaves_NeverThrowAndLeaveValidFile()
    {
        var store = new JsonFileAccountStore(new TestProtector());
        (Server server, _) = SampleAccount();
        var data = new AppData { Servers = { server } };

        Task[] saves = Enumerable.Range(0, 8)
            .Select(_ => store.SaveAsync(_file, data))
            .ToArray();
        await Task.WhenAll(saves);

        AppData loaded = await store.LoadAsync(_file);
        Assert.Single(loaded.Servers);
        Assert.False(File.Exists(_file + ".tmp"));
    }
}
