using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PwAssistant.App.Services;
using PwAssistant.App.Views;
using PwAssistant.Core.Launcher;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;
using AppStrings = PwAssistant.App.Resources.Strings;

namespace PwAssistant.App.ViewModels;

public sealed partial class AccountCard : ObservableObject
{
    public Account Model { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(PlayText))]
    private AccountStatus status;

    public AccountCard(Account model)
    {
        Model = model;
        status = model.Status;
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Model.Nickname)
        ? (string.IsNullOrWhiteSpace(Model.Role) ? Model.Login : Model.Role)
        : Model.Nickname;
    public string StatusText => Status == AccountStatus.Online ? "Online" : "Offline";

    public string PlayText => Status == AccountStatus.Online ? AppStrings.Stop : AppStrings.Play;

    public string? ClassImagePath => string.IsNullOrWhiteSpace(Model.Class)
        ? null
        : $"/PwAssistant.App;component/Resources/Classes/{Model.Class.Trim().ToLowerInvariant()}.png";

    public string? ClassBadgeText
    {
        get
        {
            if (!ClassCatalog.TryGet(Model.Class, out ClassInfo info))
                return null;
            return $"{info.DisplayName} ({info.Abbreviation})";
        }
    }

    public void Refresh() => Status = Model.Status;
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly GameLauncher _launcher;
    private readonly PresetDispatcher _dispatcher;
    private readonly FileLogger _log;
    private readonly IServiceProvider _services;

    public ObservableCollection<Server> Servers { get; } = new();
    public ObservableCollection<AccountCard> Accounts { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountsHeader))]
    private Server? selectedServer;

    public string AccountsHeader => SelectedServer is null
        ? AppStrings.NoServerSelected
        : string.Format(AppStrings.AccountsOfServer, SelectedServer.Name);

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public string EditText => AppStrings.Edit;
    public string DeleteText => AppStrings.Delete;
    public string CopyText => AppStrings.Copy;

    public MainViewModel(AppState state, GameLauncher launcher, PresetDispatcher dispatcher, FileLogger log, IServiceProvider services)
    {
        _state = state;
        _launcher = launcher;
        _dispatcher = dispatcher;
        _log = log;
        _services = services;
    }

    public async Task InitializeAsync()
    {
        await _state.LoadAsync().ConfigureAwait(false);
        App.Current.Dispatcher.Invoke(() =>
        {
            Servers.Clear();
            foreach (Server server in _state.Data.Servers)
                Servers.Add(server);
            // Last used wins; nothing preselected on first run (raw by rule).
            SelectedServer = Servers.FirstOrDefault(s => s.Id == _state.Data.LastSelectedServerId);
        });
    }

    partial void OnSelectedServerChanged(Server? value)
    {
        _state.SelectedServer = value;
        _state.Data.LastSelectedServerId = value?.Id;
        Accounts.Clear();
        if (value is null) return;
        foreach (Account account in value.Accounts)
            Accounts.Add(new AccountCard(account));
        _ = PersistSelectionAsync();
    }

    private async Task PersistSelectionAsync()
    {
        try
        {
            await _state.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AddServerAsync()
    {
        var dialog = new ServerDialog();
        if (dialog.ShowDialog() == true)
        {
            var server = new Server { Name = dialog.ServerName, ElementClientPath = dialog.ClientPath };
            _state.Data.Servers.Add(server);
            Servers.Add(server);
            SelectedServer = server;
            await _state.SaveAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        if (SelectedServer is null) return;
        var dialog = new AccountDialog();
        if (dialog.ShowDialog() == true)
        {
            var account = new Account
            {
                ServerId = SelectedServer.Id,
                Login = dialog.Login,
                Role = dialog.Role,
                Nickname = dialog.Nickname,
                Class = dialog.Class
            };
            _state.SetPassword(account, dialog.Password);
            SelectedServer.Accounts.Add(account);
            Accounts.Add(new AccountCard(account));
            await _state.SaveAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task PlayAsync(AccountCard? card)
    {
        if (card is null || SelectedServer is null) return;
        if (card.Model.Status == AccountStatus.Online)
        {
            await StopClientAsync(card).ConfigureAwait(false);
            return;
        }
        try
        {
            StatusMessage = "...";
            string password = _state.RevealPassword(card.Model);
            GameSession session = await _launcher.LaunchAsync(
                SelectedServer, card.Model, password, TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            MarkClientWindow(card.Model);
            _log.Info($"Launched {card.Model.Login} pid={session.ProcessId}.");
            WatchSession(session, card);
            await _state.SaveAsync().ConfigureAwait(false);
            App.Current.Dispatcher.Invoke(card.Refresh);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _log.Error($"Launch {card?.Model.Login} failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Own taskbar button (per-account AppUserModelID) + class icon.
    /// Best effort with persisted diagnostics; never fails the launch.
    /// </summary>
    private void MarkClientWindow(Account account)
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

    /// <summary>
    /// Graceful client close (like the window X button), Kill fallback.
    /// The Exited handler finishes the job (jobs canceled, card offline).
    /// </summary>
    private async Task StopClientAsync(AccountCard card)
    {
        if (card.Model.ProcessId is not int pid) return;
        try
        {
            using Process process = Process.GetProcessById(pid);
            process.CloseMainWindow();
            for (int i = 0; i < 20 && !process.HasExited; i++)
                await Task.Delay(250).ConfigureAwait(false);
            if (!process.HasExited)
                process.Kill();
            _log.Info($"Stopped {card.Model.Login} pid={pid}.");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _log.Error($"Stop {card.Model.Login} failed: {ex.Message}");
        }
    }

    /// <summary>
    /// M6 crash handling: when the client dies, cancel running jobs and
    /// mark the account offline.
    /// </summary>
    private void WatchSession(GameSession session, AccountCard card)
    {
        try
        {
            Process process = Process.GetProcessById(session.ProcessId);
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                _log.Info($"Client pid={session.ProcessId} exited.");
                _dispatcher.CancelAll();
                card.Model.ProcessId = null;
                card.Model.WindowHandle = IntPtr.Zero;
                App.Current.Dispatcher.Invoke(card.Refresh);
            };
        }
        catch (ArgumentException)
        {
            // Process already gone; status refresh will show it offline.
        }
    }

    [RelayCommand]
    private async Task EditAccountAsync(AccountCard? card)
    {
        if (card is null || SelectedServer is null) return;
        var dialog = new AccountDialog { RequirePassword = false };
        dialog.Prefill(card.Model);
        if (dialog.ShowDialog() != true) return;
        card.Model.Login = dialog.Login;
        card.Model.Role = dialog.Role;
        card.Model.Nickname = dialog.Nickname;
        card.Model.Class = dialog.Class;
        if (!string.IsNullOrEmpty(dialog.Password))
            _state.SetPassword(card.Model, dialog.Password);
        await _state.SaveAsync().ConfigureAwait(false);
        App.Current.Dispatcher.Invoke(() => OnSelectedServerChanged(SelectedServer));
    }

    [RelayCommand]
    private async Task DeleteAccountAsync(AccountCard? card)
    {
        if (card is null || SelectedServer is null) return;
        Guid id = card.Model.Id;
        SelectedServer.Accounts.Remove(card.Model);
        foreach (Group group in _state.Data.Groups)
            group.AccountIds.Remove(id);
        foreach (Preset preset in _state.Data.Presets)
            preset.Actions.RemoveAll(a => a.AccountId == id);
        await _state.SaveAsync().ConfigureAwait(false);
        App.Current.Dispatcher.Invoke(() => OnSelectedServerChanged(SelectedServer));
    }

    [RelayCommand]
    private void CopyLogin(AccountCard? card)
    {
        if (card is null) return;
        // Clipboard is readable by other apps; same exposure as in-memory
        // reveal at dispatch. Standard manager behavior, user-initiated.
        // Auto-cleared below so the secret does not linger.
        Clipboard.SetText(card.Model.Login);
        ClearClipboardAfter(TimeSpan.FromSeconds(30), card.Model.Login);
    }

    [RelayCommand]
    private void CopyPassword(AccountCard? card)
    {
        if (card is null) return;
        // See CopyLogin re clipboard exposure.
        string password = _state.RevealPassword(card.Model);
        Clipboard.SetText(password);
        ClearClipboardAfter(TimeSpan.FromSeconds(30), password);
    }

    private static void ClearClipboardAfter(TimeSpan delay, string expected)
    {
        Task.Delay(delay).ContinueWith(_ =>
        {
            try
            {
                if (Clipboard.GetText() == expected)
                    Clipboard.Clear();
            }
            catch (Exception)
            {
                // Clipboard busy: leave the value; the next copy overwrites it.
            }
        }, TaskScheduler.FromCurrentSynchronizationContext());
    }

    [RelayCommand]
    private void OpenGroupMode()
    {
        var window = (GroupWindow)_services.GetService(typeof(GroupWindow))!;
        window.Show();
    }

    [RelayCommand]
    private void RefreshStatus()
    {
        _state.RefreshOnlineStatus();
        foreach (AccountCard card in Accounts)
            card.Refresh();
    }
}
