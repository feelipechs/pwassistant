using System.Diagnostics;
using System.IO;
using PwHelper.Core.Input;
using PwHelper.Core.Models;
using PwHelper.Core.Storage;

namespace PwHelper.App.Services;

/// <summary>
/// In-memory session: persisted data + live process/HWND state.
/// Runtime fields live on the Account models; only secrets+structure persist.
/// </summary>
public sealed class AppState
{
    private readonly IAccountStore _store;
    private readonly IWindowResolver _resolver;
    private readonly string _filePath;

    public AppData Data { get; private set; } = new();
    public Server? SelectedServer { get; set; }

    public AppState(IAccountStore store, IWindowResolver resolver, string filePath)
    {
        _store = store;
        _resolver = resolver;
        _filePath = filePath;
    }

    public static string DefaultFilePath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PwHelper", "accounts.json");

    public async Task LoadAsync()
    {
        if (File.Exists(_filePath))
            Data = await _store.LoadAsync(_filePath).ConfigureAwait(false);
        SelectedServer = Data.Servers.FirstOrDefault();
        RefreshOnlineStatus();
    }

    public Task SaveAsync() => _store.SaveAsync(_filePath, Data);

    public string RevealPassword(Account account) => _store.RevealPassword(account);

    public void SetPassword(Account account, string plaintext) => _store.SetPassword(account, plaintext);

    /// <summary>Re-resolve PID → HWND for every tracked account (never cached).</summary>
    public void RefreshOnlineStatus()
    {
        foreach (Account account in Data.Servers.SelectMany(s => s.Accounts))
        {
            if (account.ProcessId is null)
            {
                account.WindowHandle = IntPtr.Zero;
                continue;
            }

            try
            {
                Process.GetProcessById(account.ProcessId.Value);
            }
            catch (ArgumentException)
            {
                account.ProcessId = null;
                account.WindowHandle = IntPtr.Zero;
                continue;
            }

            account.WindowHandle = _resolver.ResolveWindow(account.ProcessId.Value);
        }
    }

    public IWindowTarget? ResolveTarget(Guid accountId)
    {
        Account? account = Data.Servers
            .SelectMany(s => s.Accounts)
            .FirstOrDefault(a => a.Id == accountId);
        if (account is null || account.WindowHandle == IntPtr.Zero)
            return null;
        return new AccountWindowTarget(account.Id, account.WindowHandle);
    }

    private sealed record AccountWindowTarget(Guid AccountId, IntPtr WindowHandle) : IWindowTarget;
}
