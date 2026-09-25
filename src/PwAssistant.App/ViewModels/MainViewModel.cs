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
    [NotifyPropertyChangedFor(nameof(PlayGlyph))]
    private AccountStatus status;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PasswordDisplay))]
    [NotifyPropertyChangedFor(nameof(PasswordToggleHint))]
    private bool isPasswordRevealed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PlayGlyph))]
    private bool isLaunching;

    private string? _revealedPassword;

    public AccountCard(Account model)
    {
        Model = model;
        status = model.Status;
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Model.Nickname)
        ? (string.IsNullOrWhiteSpace(Model.Role) ? Model.Login : Model.Role)
        : Model.Nickname;
    public string StatusText => Status == AccountStatus.Online ? "Online" : "Offline";

    public Views.StatusKind DisplayStatus =>
        Status == AccountStatus.Online ? Views.StatusKind.Online : Views.StatusKind.Offline;

    public string PlayText => Status == AccountStatus.Online ? AppStrings.Stop : AppStrings.Play;

    /// <summary>Segoe MDL2 play/stop glyphs (icon font applied in XAML);
    /// launching shows a refresh glyph spun by a storyboard trigger.</summary>
    public string PlayGlyph => IsLaunching ? "\uE72C" : Status == AccountStatus.Online ? "\uE71A" : "\uE768";

    public string PasswordToggleHint =>
        IsPasswordRevealed ? AppStrings.HidePassword : AppStrings.ShowPassword;

    public string? RevealedPassword => _revealedPassword;

    /// <summary>Masked dots, or the plaintext while revealed (auto-hides).</summary>
    public string PasswordDisplay =>
        IsPasswordRevealed && _revealedPassword is not null ? _revealedPassword : "••••••";

    /// <summary>Reveal in place; the plaintext never goes to log/disk.</summary>
    public void RevealPassword(string plaintext)
    {
        _revealedPassword = plaintext;
        IsPasswordRevealed = true;
    }

    public void HidePassword()
    {
        _revealedPassword = null;
        IsPasswordRevealed = false;
    }

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

/// <summary>Sidebar row: server name plus live account/online counts.</summary>
public sealed partial class ServerRow : ObservableObject
{
    public Server Model { get; }

    public ServerRow(Server model) => Model = model;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    [NotifyPropertyChangedFor(nameof(HasOnline))]
    private int onlineCount;

    public string Name => Model.Name;
    public int Total => Model.Accounts.Count;
    public bool HasOnline => OnlineCount > 0;
    public string StatsText => AppStrings.ServerStats(Total, OnlineCount);

    public Views.StatusKind DisplayStatus =>
        HasOnline ? Views.StatusKind.Online : Views.StatusKind.Offline;

    /// <summary>Recounts online (call after AppState.RefreshOnlineStatus).</summary>
    public void Refresh() => OnlineCount = Model.Accounts.Count(a => a.Status == AccountStatus.Online);

    public void NotifyRenamed() => OnPropertyChanged(nameof(Name));
}

/// <summary>One tab-strip entry. Null Tab means the implicit "All" tab.</summary>
public sealed partial class TabItem : ObservableObject
{
    public AccountTab? Tab { get; }
    public bool IsAll => Tab is null;

    public TabItem(AccountTab? tab, string title)
    {
        Tab = tab;
        Title = title;
    }

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private bool isSelected;

    public void RefreshTitle()
    {
        if (Tab is not null)
            Title = Tab.Name;
    }
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly GameLauncher _launcher;
    private readonly PresetDispatcher _dispatcher;
    private readonly FileLogger _log;
    private readonly IServiceProvider _services;
    private readonly FocusController _focus;

    /// <summary>Watched client processes, rooted so Exited always fires.</summary>
    private readonly Dictionary<int, Process> _watched = new();

    /// <summary>Graceful close budget: the client only ever dies by Kill,
    /// so this is just a short courtesy window before forcing it.</summary>
    private const int StopGracePolls = 10;
    private const int StopGraceMs = 100;

    public ObservableCollection<ServerRow> Servers { get; } = new();
    public ObservableCollection<AccountCard> Accounts { get; } = new();
    public ObservableCollection<AccountTab> Tabs { get; } = new();
    public ObservableCollection<TabItem> TabItems { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountsHeader))]
    private ServerRow? selectedServer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTabSelected))]
    private AccountTab? selectedTab;

    /// <summary>True when a named tab (not "All") filters the cards.</summary>
    public bool IsTabSelected => SelectedTab is not null;

    public string AccountsHeader => SelectedServer is null
        ? AppStrings.NoServerSelected
        : string.Format(AppStrings.AccountsOfServer, SelectedServer.Name);

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public string EditText => AppStrings.Edit;
    public string DeleteText => AppStrings.Delete;
    public string CopyText => AppStrings.Copy;
    public string CopyLoginText => AppStrings.CopyLogin;
    public string CopyPasswordText => AppStrings.CopyPassword;

    public MainViewModel(AppState state, GameLauncher launcher, PresetDispatcher dispatcher, FileLogger log, IServiceProvider services, FocusController focus)
    {
        _state = state;
        _launcher = launcher;
        _dispatcher = dispatcher;
        _log = log;
        _services = services;
        _focus = focus;
    }

    public async Task InitializeAsync()
    {
        await _state.LoadAsync().ConfigureAwait(false);
        if (MigrateTabsToTags())
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
        App.Current.Dispatcher.Invoke(() =>
        {
            Servers.Clear();
            foreach (Server server in _state.Data.Servers)
                Servers.Add(new ServerRow(server));
            // Last used wins; nothing preselected on first run (raw by rule).
            SelectedServer = Servers.FirstOrDefault(s => s.Model.Id == _state.Data.LastSelectedServerId);
            RefreshServerRows();
            // STA thread: scrubs launch-credential residue from .lnk files
            // (an interrupted Play may have left secrets on disk).
            try
            {
                int cleansed = _launcher.CleanseAllShortcuts(_state.Data.Servers);
                if (cleansed > 0)
                    _log.Info($"Cleansed {cleansed} client shortcuts.");
            }
            catch (Exception ex)
            {
                _log.Warn($"Shortcut cleanse failed: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// One-time migration: legacy tab AccountIds become Account.Tag
    /// (first-claim wins); membership is Tag-based from then on.
    /// </summary>
    private bool MigrateTabsToTags()
    {
        bool changed = false;
        var byId = _state.Data.Servers.SelectMany(s => s.Accounts).ToDictionary(a => a.Id);
        foreach (AccountTab tab in _state.Data.Tabs)
        {
            foreach (Guid id in tab.AccountIds)
            {
                if (byId.TryGetValue(id, out Account? account) && string.IsNullOrWhiteSpace(account.Tag))
                {
                    account.Tag = tab.Name;
                    changed = true;
                }
            }
            if (tab.AccountIds.Count > 0)
            {
                tab.AccountIds.Clear();
                changed = true;
            }
        }
        return changed;
    }

    partial void OnSelectedServerChanged(ServerRow? value)
    {
        _state.SelectedServer = value?.Model;
        _state.Data.LastSelectedServerId = value?.Model.Id;
        RebuildTabs();
        RebuildAccounts();
        _ = PersistSelectionAsync();
    }

    partial void OnSelectedTabChanged(AccountTab? value)
    {
        foreach (TabItem item in TabItems)
            item.IsSelected = item.Tab == value;
        RebuildAccounts();
    }

    [RelayCommand]
    private void SelectTab(TabItem? item)
    {
        if (item is null) return;
        SelectedTab = item.Tab;
        foreach (TabItem entry in TabItems)
            entry.IsSelected = entry == item;
    }

    private void RebuildTabs()
    {
        Tabs.Clear();
        TabItems.Clear();
        SelectedTab = null;
        if (SelectedServer is null) return;
        TabItems.Add(new TabItem(null, AppStrings.All) { IsSelected = true });
        foreach (AccountTab tab in _state.Data.Tabs.Where(t => t.ServerId == SelectedServer.Model.Id))
        {
            Tabs.Add(tab);
            TabItems.Add(new TabItem(tab, tab.Name));
        }
    }

    private void RebuildAccounts()
    {
        Accounts.Clear();
        if (SelectedServer is null) return;
        IEnumerable<Account> scope = SelectedTab is null
            ? SelectedServer.Model.Accounts
            : SelectedServer.Model.Accounts.Where(a =>
                string.Equals(a.Tag, SelectedTab.Name, StringComparison.OrdinalIgnoreCase));
        foreach (Account account in scope)
            Accounts.Add(new AccountCard(account));
    }

    private void RefreshServerRows()
    {
        _state.RefreshOnlineStatus();
        foreach (ServerRow row in Servers)
            row.Refresh();
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
        if (Application.Current.MainWindow is not MainWindow main) return;
        (bool ok, string name, string path) = await main.AskServerAsync(null, null);
        if (!ok) return;
        {
            var server = new Server { Name = name, ElementClientPath = path };
            _state.Data.Servers.Add(server);
            var row = new ServerRow(server);
            Servers.Add(row);
            SelectedServer = row;
            await _state.SaveAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task EditServerAsync(ServerRow? row)
    {
        if (row is null) return;
        if (Application.Current.MainWindow is not MainWindow main) return;
        (bool ok, string name, string path) = await main.AskServerAsync(row.Model.Name, row.Model.ElementClientPath);
        if (!ok) return;
        row.Model.Name = name;
        row.Model.ElementClientPath = path;
        row.NotifyRenamed();
        await _state.SaveAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task DeleteServerAsync(ServerRow? row)
    {
        if (row is null) return;
        if (Application.Current.MainWindow is not MainWindow main) return;
        if (!await main.AskConfirmAsync(
            AppStrings.DeleteServerTitle,
            AppStrings.DeleteServerConfirm(row.Model.Name, row.Model.Accounts.Count),
            AppStrings.Delete))
            return;

        var ids = row.Model.Accounts.Select(a => a.Id).ToHashSet();
        _state.Data.Servers.Remove(row.Model);
        _state.Data.Tabs.RemoveAll(t => t.ServerId == row.Model.Id);
        foreach (Group group in _state.Data.Groups)
            group.AccountIds.RemoveAll(ids.Contains);
        foreach (Preset preset in _state.Data.Presets)
            preset.Actions.RemoveAll(a => ids.Contains(a.AccountId));
        Servers.Remove(row);
        if (SelectedServer == row)
            SelectedServer = Servers.FirstOrDefault();
        RefreshServerRows();
        await _state.SaveAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        if (SelectedServer is null) return;
        if (Application.Current.MainWindow is not MainWindow main) return;
        AccountDraft? draft = await main.AskAccountAsync(
            new AccountDraft(string.Empty, string.Empty, string.Empty, null, null, null),
            _state.Data.Tabs
                .Where(t => t.ServerId == SelectedServer.Model.Id)
                .Select(t => t.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList(),
            SelectedTab?.Name,
            requirePassword: true);
        if (draft is null) return;
        {
            var account = new Account
            {
                ServerId = SelectedServer.Model.Id,
                Login = draft.Login,
                Role = draft.Role,
                Nickname = draft.Nickname,
                Class = draft.ClassKey,
                Tag = draft.Tag
            };
            _state.SetPassword(account, draft.Password);
            SelectedServer.Model.Accounts.Add(account);
            RebuildAccounts();
            RefreshServerRows();
            await _state.SaveAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task AddTabAsync()
    {
        if (SelectedServer is null) return;
        if (Application.Current.MainWindow is not MainWindow main) return;
        (bool ok, string name) = await main.AskPromptAsync("TabName", string.Empty);
        if (!ok) return;
        AccountTab? existing = _state.Data.Tabs.FirstOrDefault(t =>
            t.ServerId == SelectedServer.Model.Id &&
            string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            SelectedTab = existing;
            return;
        }
        var tab = new AccountTab { ServerId = SelectedServer.Model.Id, Name = name };
        _state.Data.Tabs.Add(tab);
        Tabs.Add(tab);
        TabItems.Add(new TabItem(tab, tab.Name));
        SelectedTab = tab;
        await _state.SaveAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RenameTabAsync(AccountTab? tab)
    {
        if (tab is null) return;
        string oldName = tab.Name;
        if (Application.Current.MainWindow is not MainWindow main) return;
        (bool ok, string name) = await main.AskPromptAsync("TabName", tab.Name);
        if (!ok) return;
        tab.Name = name;
        foreach (Account account in _state.Data.Servers
            .Where(s => s.Id == tab.ServerId)
            .SelectMany(s => s.Accounts)
            .Where(a => string.Equals(a.Tag, oldName, StringComparison.OrdinalIgnoreCase)))
            account.Tag = tab.Name;
        // ObservableCollection holds the reference; force a re-read.
        int index = Tabs.IndexOf(tab);
        if (index >= 0)
        {
            Tabs.RemoveAt(index);
            Tabs.Insert(index, tab);
        }
        foreach (TabItem item in TabItems.Where(i => i.Tab == tab))
            item.RefreshTitle();
        RebuildAccounts();
        await _state.SaveAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task DeleteTabAsync(AccountTab? tab)
    {
        if (tab is null) return;
        if (Application.Current.MainWindow is not MainWindow main) return;
        if (!await main.AskConfirmAsync(
            AppStrings.DeleteTabTitle,
            AppStrings.DeleteTabConfirm(tab.Name),
            AppStrings.Delete))
            return;
        _state.Data.Tabs.Remove(tab);
        Tabs.Remove(tab);
        foreach (Account account in _state.Data.Servers
            .Where(s => s.Id == tab.ServerId)
            .SelectMany(s => s.Accounts)
            .Where(a => string.Equals(a.Tag, tab.Name, StringComparison.OrdinalIgnoreCase)))
            account.Tag = null;
        TabItem? strip = TabItems.FirstOrDefault(i => i.Tab == tab);
        if (strip is not null)
            TabItems.Remove(strip);
        if (SelectedTab == tab)
            SelectedTab = null;
        await _state.SaveAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task RemoveFromTabAsync(AccountCard? card)
    {
        if (card is null || SelectedTab is null) return;
        card.Model.Tag = null;
        // UI-bound collections first (UI thread), persistence after:
        // touching them past ConfigureAwait(false) crashes cross-thread.
        RebuildAccounts();
        await _state.SaveAsync().ConfigureAwait(false);
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task PlayAsync(AccountCard? card)
    {
        if (card is null || SelectedServer is null) return;
        if (card.IsLaunching) return;
        if (card.Model.Status == AccountStatus.Online)
        {
            await StopClientAsync(card).ConfigureAwait(false);
            return;
        }
        try
        {
            card.IsLaunching = true;
            StatusMessage = "...";
            string password = _state.RevealPassword(card.Model);
            GameSession session = await _launcher.LaunchAsync(
                SelectedServer.Model, card.Model, password, TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            MarkClientWindow(card.Model);
            _log.Info($"Launched {card.Model.Login} pid={session.ProcessId}.");
            WatchSession(session, card);
            await _state.SaveAsync().ConfigureAwait(false);
            App.Current.Dispatcher.Invoke(() =>
            {
                card.Refresh();
                RefreshServerRows();
            });
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _log.Error($"Launch {card?.Model.Login} failed: {ex.Message}");
        }
        finally
        {
            App.Current.Dispatcher.Invoke(() => card.IsLaunching = false);
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
        // Rooted: an unrooted Process may never raise Exited (GC collects it).
        Process? owned = null;
        bool exited = false;
        try
        {
            owned = Process.GetProcessById(pid);
            lock (_watched) _watched[pid] = owned;
            owned.CloseMainWindow();
            for (int i = 0; i < StopGracePolls && !owned.HasExited; i++)
                await Task.Delay(StopGraceMs).ConfigureAwait(false);
            if (!owned.HasExited)
                owned.Kill();
            exited = true;
            _log.Info($"Stopped {card.Model.Login} pid={pid}.");
        }
        catch (ArgumentException)
        {
            // Already gone: treat as exited so the card resets below.
            exited = true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _log.Error($"Stop {card.Model.Login} failed: {ex.Message}");
        }
        if (!exited) return;
        // Explicit fallback: never rely solely on Exited to restore icons.
        card.Model.ProcessId = null;
        card.Model.WindowHandle = IntPtr.Zero;
        App.Current.Dispatcher.Invoke(() =>
        {
            card.Refresh();
            RefreshServerRows();
        });
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
            lock (_watched) _watched[session.ProcessId] = process;
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                lock (_watched) _watched.Remove(session.ProcessId);
                _log.Info($"Client pid={session.ProcessId} exited.");
                _dispatcher.CancelAll();
                _focus.RemoveAccount(card.Model.Id);
                card.Model.ProcessId = null;
                card.Model.WindowHandle = IntPtr.Zero;
                App.Current.Dispatcher.Invoke(() =>
                {
                    card.Refresh();
                    RefreshServerRows();
                });
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
        if (Application.Current.MainWindow is not MainWindow main) return;
        AccountDraft? draft = await main.AskAccountAsync(
            new AccountDraft(
                card.Model.Login, string.Empty, card.Model.Role ?? string.Empty,
                card.Model.Nickname, card.Model.Class, card.Model.Tag),
            _state.Data.Tabs
                .Where(t => t.ServerId == SelectedServer.Model.Id)
                .Select(t => t.Name)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n)
                .ToList(),
            lockTag: null,
            requirePassword: false);
        if (draft is null) return;
        card.Model.Login = draft.Login;
        card.Model.Role = draft.Role;
        card.Model.Nickname = draft.Nickname;
        card.Model.Class = draft.ClassKey;
        card.Model.Tag = draft.Tag;
        if (!string.IsNullOrEmpty(draft.Password))
            _state.SetPassword(card.Model, draft.Password);
        await _state.SaveAsync().ConfigureAwait(false);
        App.Current.Dispatcher.Invoke(RebuildAccounts);
    }

    [RelayCommand]
    private async Task DeleteAccountAsync(AccountCard? card)
    {
        if (card is null || SelectedServer is null) return;
        if (Application.Current.MainWindow is not MainWindow main) return;
        if (!await main.AskConfirmAsync(
            AppStrings.DeleteAccountTitle,
            AppStrings.DeleteAccountConfirm(card.DisplayName),
            AppStrings.Delete))
            return;
        Guid id = card.Model.Id;
        SelectedServer.Model.Accounts.Remove(card.Model);
        foreach (Group group in _state.Data.Groups)
            group.AccountIds.Remove(id);
        foreach (AccountTab tab in _state.Data.Tabs)
            tab.AccountIds.Remove(id);
        foreach (Preset preset in _state.Data.Presets)
            preset.Actions.RemoveAll(a => a.AccountId == id);
        await _state.SaveAsync().ConfigureAwait(false);
        App.Current.Dispatcher.Invoke(() =>
        {
            RebuildAccounts();
            RefreshServerRows();
        });
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

    [RelayCommand]
    private void TogglePasswordVisibility(AccountCard? card)
    {
        if (card is null) return;
        if (card.IsPasswordRevealed)
        {
            card.HidePassword();
            return;
        }
        // Same exposure class as copy-at-dispatch: user-initiated, in-memory
        // only, never logged. Auto-hides so it does not linger on screen.
        card.RevealPassword(_state.RevealPassword(card.Model));
        string? shown = card.RevealedPassword;
        Task.Delay(TimeSpan.FromSeconds(15)).ContinueWith(_ =>
        {
            if (card.IsPasswordRevealed && card.RevealedPassword == shown)
                card.HidePassword();
        }, TaskScheduler.FromCurrentSynchronizationContext());
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
        RefreshServerRows();
        foreach (AccountCard card in Accounts)
            card.Refresh();
    }
}
