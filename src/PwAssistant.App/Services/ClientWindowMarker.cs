using System.IO;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.App.Services;

/// <summary>
/// Own taskbar button (per-account AppUserModelID) + class icon.
/// Best effort with persisted diagnostics; never fails the launch.
/// Keeps HWND/WinApi access out of the ViewModels.
/// </summary>
public sealed class ClientWindowMarker
{
    private readonly FileLogger _log;

    public ClientWindowMarker(FileLogger log) => _log = log;

    public void Mark(Account account)
    {
        bool iconOk = false;
        if (ClassCatalog.TryGet(account.Class, out ClassInfo info))
        {
            string iconPath = Path.Combine(
                AppContext.BaseDirectory, "Resources", "Classes", info.Key + ".ico");
            iconOk = WindowIcon.TrySetIcon(account.WindowHandle, iconPath);
        }
        _log.Info($"Marked {account.Login}: icon={iconOk}.");
        if (!iconOk)
            _ = RetryIconAsync(account);
    }

    /// <summary>Best effort: the icon may only stick once settled.</summary>
    private async Task RetryIconAsync(Account account)
    {
        try
        {
            for (int i = 0; i < 5; i++)
            {
                await Task.Delay(2000).ConfigureAwait(false);
                if (account.WindowHandle == IntPtr.Zero)
                    return;
                if (!ClassCatalog.TryGet(account.Class, out ClassInfo info))
                    return;
                string iconPath = Path.Combine(
                    AppContext.BaseDirectory, "Resources", "Classes", info.Key + ".ico");
                if (WindowIcon.TrySetIcon(account.WindowHandle, iconPath))
                {
                    _log.Info($"Marked {account.Login} on retry: icon=True.");
                    return;
                }
            }
            _log.Warn($"Marked {account.Login}: icon still False.");
        }
        catch (Exception ex)
        {
            _log.Error($"Icon retry {account.Login} failed: {ex.Message}");
        }
    }
}
