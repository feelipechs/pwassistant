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

        foreach (Account account in AllOnlineAccounts())
        {
            if (account.WindowHandle == IntPtr.Zero) continue;
            if (IsPointInside(account.WindowHandle, click.ScreenX, click.ScreenY, out int cx, out int cy))
            {
                // Click landed on this account's window: it is the master.
                _ = ReplicateAsync(account, cx, cy);
                return;
            }
        }
    }

    private async Task ReplicateAsync(Account master, int screenX, int screenY)
    {
        (int Width, int Height) size;
        (int X, int Y) client;
        try
        {
            size = _resolver.GetClientSize(master.WindowHandle);
            client = _resolver.ScreenToClientPoint(master.WindowHandle, screenX, screenY);
        }
        catch (WinApiException)
        {
            return;
        }

        RelativePosition? fraction = _sync.CaptureMasterClick(
            client.X, client.Y, size.Width, size.Height, isLeftButton: true);
        if (fraction is null) return;

        foreach (Account replica in AllOnlineAccounts().Where(a => a.Id != master.Id && _sync.IsSynced(a.Id)))
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
