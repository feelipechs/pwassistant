using PwHelper.Core.Input;
using PwHelper.Core.Models;
using PwHelper.Core.Sync;
using PwHelper.WinApi;

namespace PwHelper.App.Services;

/// <summary>
/// Sync Click wiring (M4): physical master click → fraction → fan-out as
/// UI clicks on every other synced online account. Only the left button,
/// only inside a registered game window, only while enabled.
/// </summary>
public sealed class SyncController : IDisposable
{
    private readonly MouseHook _hook;
    private readonly SyncService _sync;
    private readonly AppState _state;
    private readonly IInputStrategy _strategy;
    private readonly IWindowResolver _resolver;
    private bool _disposed;

    public SyncController(
        MouseHook hook, SyncService sync, AppState state,
        IInputStrategy strategy, IWindowResolver resolver)
    {
        _hook = hook;
        _sync = sync;
        _state = state;
        _strategy = strategy;
        _resolver = resolver;
        _hook.LeftButtonDown += OnLeftButtonDown;
    }

    public bool Enabled
    {
        get => _sync.Enabled;
        set => _sync.SetEnabled(value);
    }

    public void Start() => _hook.Start();
    public void Stop() => _hook.Stop();

    public void SetSyncedAccounts(IEnumerable<Guid> accountIds) => _sync.SetSyncedAccounts(accountIds);

    private void OnLeftButtonDown(MasterClick click)
    {
        if (!_sync.Enabled) return;

        List<Account> online = AllOnlineAccounts().ToList();
        Account? master = FindTopmostAccount(online, click.ScreenX, click.ScreenY)
            ?? FindFirstContainingAccount(online, click.ScreenX, click.ScreenY);
        if (master is null) return;

        (int Width, int Height) size;
        (int X, int Y) client;
        try
        {
            size = _resolver.GetClientSize(master.WindowHandle);
            client = _resolver.ScreenToClientPoint(master.WindowHandle, click.ScreenX, click.ScreenY);
        }
        catch (WinApiException)
        {
            return;
        }

        _ = ReplicateAsync(master, online, client.X, client.Y, size.Width, size.Height);
    }

    /// <summary>Master = the topmost registered game window at the click point.</summary>
    private Account? FindTopmostAccount(List<Account> online, int screenX, int screenY)
    {
        IntPtr top;
        try
        {
            top = _resolver.ResolveTopWindowAtPoint(screenX, screenY);
        }
        catch (WinApiException)
        {
            return null;
        }
        if (top == IntPtr.Zero) return null;
        return online.FirstOrDefault(a => a.WindowHandle == top);
    }

    /// <summary>Fallback when the topmost window is not a tracked game window.</summary>
    private Account? FindFirstContainingAccount(List<Account> online, int screenX, int screenY)
    {
        foreach (Account account in online)
        {
            if (account.WindowHandle == IntPtr.Zero) continue;
            if (IsPointInside(account.WindowHandle, screenX, screenY, out _, out _))
                return account;
        }
        return null;
    }

    private async Task ReplicateAsync(
        Account master, List<Account> online, int clientX, int clientY, int clientWidth, int clientHeight)
    {
        RelativePosition? fraction = _sync.CaptureMasterClick(
            clientX, clientY, clientWidth, clientHeight, isLeftButton: true);
        if (fraction is null) return;

        foreach (Account replica in online.Where(a => a.Id != master.Id && _sync.IsSynced(a.Id)))
        {
            try
            {
                await _strategy.SendUiClickAsync(
                    new ReplicaTarget(replica.Id, replica.WindowHandle),
                    fraction.X, fraction.Y).ConfigureAwait(false);
            }
            catch (WinApiException)
            {
                // One failing replica never breaks the fan-out.
            }
        }
    }

    private IEnumerable<Account> AllOnlineAccounts() =>
        _state.Data.Servers.SelectMany(s => s.Accounts)
            .Where(a => a.WindowHandle != IntPtr.Zero);

    private bool IsPointInside(IntPtr hwnd, int screenX, int screenY, out int clientX, out int clientY)
    {
        clientX = clientY = 0;
        try
        {
            (int Width, int Height) size = _resolver.GetClientSize(hwnd);
            (clientX, clientY) = _resolver.ScreenToClientPoint(hwnd, screenX, screenY);
            return clientX >= 0 && clientY >= 0 && clientX <= size.Width && clientY <= size.Height;
        }
        catch (WinApiException)
        {
            return false;
        }
    }

    private sealed record ReplicaTarget(Guid AccountId, IntPtr WindowHandle) : IWindowTarget;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hook.LeftButtonDown -= OnLeftButtonDown;
        _hook.Dispose();
        GC.SuppressFinalize(this);
    }
}
