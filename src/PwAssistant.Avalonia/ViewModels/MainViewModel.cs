using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PwAssistant.Avalonia.Services;
using PwAssistant.Avalonia.Views;
using PwAssistant.Core.Launcher;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using AppStrings = PwAssistant.Avalonia.Resources.Strings;

namespace PwAssistant.Avalonia.ViewModels;

public sealed partial class AccountCard : ObservableObject
{
    public Account Model { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(DisplayStatus))]
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

    /// <summary>
    /// Tab filter flag (SPA-like cache): Accounts always holds every card of
    /// the server exactly once; the container collapses when false, so tab
    /// switches never re-realize anything. Set only by RebuildAccounts.
    /// </summary>
    [ObservableProperty]
    private bool isVisibleInTab = true;

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
        : $"avares://PwAssistant.Avalonia/Resources/Classes/{Model.Class.Trim().ToLowerInvariant()}.png";

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

    /// <summary>Refreshes Model-derived bindings after an edit (cards are
    /// now reused across rebuilds instead of recreated).</summary>
    public void RefreshIdentity()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(ClassImagePath));
        OnPropertyChanged(nameof(ClassBadgeText));
    }
}

/// <summary>Sidebar row: server name plus live account/online counts.</summary>
public sealed partial class ServerRow : ObservableObject
{
    public Server Model { get; }

    public ServerRow(Server model) => Model = model;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatsText))]
    [NotifyPropertyChangedFor(nameof(DisplayStatus))]
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
    private readonly FocusController _focus;
    private readonly IDialogService _dialogs;
    private readonly ClientWindowMarker _marker;

    /// <summary>Watched client processes, rooted so Exited always fires.</summary>
    private readonly Dictionary<int, Process> _watched = new();

    /// <summary>Graceful close budget: the client only ever dies by Kill,
    /// so this is just a short courtesy window before forcing it.</summary>
    private const int StopGracePolls = 10;
    private const int StopGraceMs = 100;

    /// <summary>Settle gap between bulk launches: LaunchAsync already waits
    /// for the window, so this is only breathing room (adaptive per machine).</summary>
    private const int BulkSettleMs = 2000;

    private CancellationTokenSource? _bulkCts;

    /// <summary>True while a bulk open/close runs: death handlers skip
    /// per-client UI refresh, toasts and focus fallback (one pass at the end).</summary>
    private bool _bulkClosing;

    /// <summary>Model-level launch guard: survives card rebuilds (a second
    /// Play on a fresh card must not fork a duplicate client).</summary>
    private readonly HashSet<Guid> _launching = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BulkActionText))]
    private bool isBulkRunning;

    /// <summary>Bulk toggle label for the selected tab (never "All"):
    /// cancel while running, close when all online, open otherwise.
    /// Only tab-visible cards count (Accounts always holds every card).</summary>
    public string BulkActionText
    {
        get
        {
            if (IsBulkRunning)
                return AppStrings.CancelDialog;
            if (SelectedServer is null || SelectedTab is null)
                return AppStrings.OpenAll;
            List<AccountCard> cards = Accounts.Where(c => c.IsVisibleInTab).ToList();
            if (cards.Count == 0)
                return AppStrings.OpenAll;
            return cards.All(c => c.Model.Status == AccountStatus.Online)
                ? AppStrings.CloseAll
                : AppStrings.OpenAll;
        }
    }

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

    public MainViewModel(AppState state, GameLauncher launcher, PresetDispatcher dispatcher, FileLogger log, FocusController focus, IDialogService dialogs, ClientWindowMarker marker)
    {
        _state = state;
        _launcher = launcher;
        _dispatcher = dispatcher;
        _log = log;
        _focus = focus;
        _dialogs = dialogs;
        _marker = marker;
    }

    /// <summary>Main-window clipboard (null when no desktop lifetime yet).</summary>
    private static IClipboard? GetClipboard()
        => ((IClassicDesktopStyleApplicationLifetime?)Application.Current?.ApplicationLifetime)?.MainWindow?.Clipboard;

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
        // STA thread (COM shortcut APIs require it): scrubs launch-credential
        // residue from .lnk files (an interrupted Play may have left secrets
        // on disk). Runs on a dedicated background STA thread — never the UI
        // thread, never the MTA pool. Only the log lines below stay on UI.
        int cleansed = 0;
        string? cleanseError = null;
        var cleanseDone = new TaskCompletionSource<(int Count, string? Error)>();
        var cleanseThread = new Thread(() =>
        {
            try
            {
                cleanseDone.SetResult((_launcher.CleanseAllShortcuts(_state.Data.Servers), null));
            }
            catch (Exception ex)
            {
                cleanseDone.SetResult((0, ex.Message));
            }
        });
        cleanseThread.SetApartmentState(ApartmentState.STA);
        cleanseThread.IsBackground = true;
        cleanseThread.Start();
        (cleansed, cleanseError) = await cleanseDone.Task.ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Servers.Clear();
            foreach (Server server in _state.Data.Servers)
                Servers.Add(new ServerRow(server));
            // Last used wins; nothing preselected on first run (raw by rule).
            SelectedServer = Servers.FirstOrDefault(s => s.Model.Id == _state.Data.LastSelectedServerId);
            RefreshServerRows();
            if (cleansed > 0)
                _log.Info($"Cleansed {cleansed} client shortcuts.");
            if (cleanseError is not null)
                _log.Warn($"Shortcut cleanse failed: {cleanseError}.");
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

    /// <summary>
    /// Membership reconcile (add/remove/reorder, containers realized once)
    /// plus the tab filter as a visibility flag: switching tabs only flips
    /// booleans, never touches the collection — the "All" tab lag is gone.
    /// Session state (launching spinner, revealed password) always survives.
    /// </summary>
    private void RebuildAccounts()
    {
        if (SelectedServer is null)
        {
            Accounts.Clear();
            OnPropertyChanged(nameof(BulkActionText));
            return;
        }
        List<Account> wanted = SelectedServer.Model.Accounts.ToList();
        var wantedIds = new HashSet<Guid>(wanted.Select(a => a.Id));
        for (int i = Accounts.Count - 1; i >= 0; i--)
            if (!wantedIds.Contains(Accounts[i].Model.Id))
                Accounts.RemoveAt(i);
        var byId = Accounts.ToDictionary(c => c.Model.Id);
        for (int i = 0; i < wanted.Count; i++)
        {
            if (byId.TryGetValue(wanted[i].Id, out AccountCard? existing))
            {
                int old = Accounts.IndexOf(existing);
                if (old != i)
                    Accounts.Move(old, i);
                existing.Refresh();
                existing.RefreshIdentity();
            }
            else
            {
                var card = new AccountCard(wanted[i]);
                Accounts.Insert(i, card);
                byId[wanted[i].Id] = card;
            }
        }
        foreach (AccountCard card in Accounts)
            card.IsVisibleInTab = SelectedTab is null || string.Equals(
                card.Model.Tag, SelectedTab.Name, StringComparison.OrdinalIgnoreCase);
        OnPropertyChanged(nameof(BulkActionText));
    }

    private void RefreshServerRows()
    {
        _state.RefreshOnlineStatus();
        foreach (ServerRow row in Servers)
            row.Refresh();
        OnPropertyChanged(nameof(BulkActionText));
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
        (bool ok, string name, string path) = await _dialogs.AskServerAsync(null, null);
        if (!ok) return;
        {
            var server = new Server { Name = name, ElementClientPath = path };
            _state.Data.Servers.Add(server);
            var row = new ServerRow(server);
            Servers.Add(row);
            SelectedServer = row;
            await _state.SaveAsync().ConfigureAwait(false);
            ToastService.Show(AppStrings.ToastAdded(name));
        }
    }

    [RelayCommand]
    private async Task EditServerAsync(ServerRow? row)
    {
        if (row is null) return;
        (bool ok, string name, string path) = await _dialogs.AskServerAsync(row.Model.Name, row.Model.ElementClientPath);
        if (!ok) return;
        row.Model.Name = name;
        row.Model.ElementClientPath = path;
        row.NotifyRenamed();
        await _state.SaveAsync().ConfigureAwait(false);
        ToastService.Show(AppStrings.ToastSaved(name));
    }

    [RelayCommand]
    private async Task DeleteServerAsync(ServerRow? row)
    {
        if (row is null) return;
        if (!await _dialogs.AskConfirmAsync(
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
        ToastService.Show(AppStrings.ToastRemoved(row.Model.Name));
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        if (SelectedServer is null) return;
        AccountDraft? draft = await _dialogs.AskAccountAsync(
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
            ToastService.Show(AppStrings.ToastAdded(draft.Login));
        }
    }

    [RelayCommand]
    private async Task AddTabAsync()
    {
        if (SelectedServer is null) return;
        (bool ok, string name) = await _dialogs.AskPromptAsync("TabName", string.Empty);
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
        ToastService.Show(AppStrings.ToastAdded(name));
    }

    [RelayCommand]
    private async Task RenameTabAsync(AccountTab? tab)
    {
        if (tab is null) return;
        string oldName = tab.Name;
        (bool ok, string name) = await _dialogs.AskPromptAsync("TabName", tab.Name);
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
        if (!await _dialogs.AskConfirmAsync(
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
        ToastService.Show(AppStrings.ToastRemoved(tab.Name));
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
        await PlayOneAsync(card).ConfigureAwait(false);
    }

    /// <summary>Launches one offline client (no toggle). Shared by manual
    /// Play and bulk open. Returns true when the client launched.</summary>
    private async Task<bool> PlayOneAsync(AccountCard card)
    {
        if (card.Model.Status == AccountStatus.Online) return false;
        lock (_launching)
        {
            if (!_launching.Add(card.Model.Id)) return false;
        }
        try
        {
            card.IsLaunching = true;
            StatusMessage = "...";
            string password = _state.RevealPassword(card.Model);
            GameSession session = await _launcher.LaunchAsync(
                SelectedServer!.Model, card.Model, password, TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            _marker.Mark(card.Model);
            _log.Info($"Launched {card.Model.Login} pid={session.ProcessId}.");
            WatchSession(session, card);
            await _state.SaveAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                card.Refresh();
                RefreshServerRows();
            });
            StatusMessage = string.Empty;
            ToastService.Show(AppStrings.ToastOpened(card.Model.Login));
            return true;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _log.Error($"Launch {card?.Model.Login} failed: {ex.Message}");
            ToastService.Show(AppStrings.ToastFailed(card?.Model.Login ?? "?", ex.Message), ToastKind.Error);
            return false;
        }
        finally
        {
            lock (_launching)
                _launching.Remove(card.Model.Id);
            await Dispatcher.UIThread.InvokeAsync(() => card.IsLaunching = false);
        }
    }

    /// <summary>
    /// Bulk open/close for the selected tab only (never "All"): opens every
    /// offline account in sequence (next starts once the previous window is
    /// up, plus a settle gap — adaptive per machine) or closes every online
    /// one. A second click cancels.
    /// </summary>
    [RelayCommand]
    private async Task OpenCloseAllInTabAsync()
    {
        if (IsBulkRunning)
        {
            _bulkCts?.Cancel();
            return;
        }
        if (SelectedServer is null || SelectedTab is null) return;
        List<AccountCard> cards = Accounts.Where(c => c.IsVisibleInTab).ToList();
        if (cards.Count == 0) return;

        _state.RefreshOnlineStatus();
        foreach (AccountCard card in cards)
            card.Refresh();
        bool allOnline = cards.All(c => c.Model.Status == AccountStatus.Online);

        _bulkCts = new CancellationTokenSource();
        IsBulkRunning = true;
        _bulkClosing = true;
        _focus.SuppressFallback = true;
        try
        {
            int done = 0;
            foreach (AccountCard card in cards)
            {
                _bulkCts.Token.ThrowIfCancellationRequested();
                StatusMessage = $"{++done}/{cards.Count}…";
                if (allOnline)
                {
                    if (card.Model.Status == AccountStatus.Online)
                        await StopClientAsync(card).ConfigureAwait(false);
                }
                else
                {
                    if (card.Model.Status == AccountStatus.Online || IsLaunchingNow(card.Model.Id))
                        continue;
                    await PlayOneAsync(card).ConfigureAwait(false);
                    await Task.Delay(BulkSettleMs, _bulkCts.Token).ConfigureAwait(false);
                }
            }
            StatusMessage = string.Empty;
            _log.Info($"Bulk {(allOnline ? "close" : "open")} tab {SelectedTab.Name}: {cards.Count} accounts.");
        }
        catch (OperationCanceledException)
        {
            StatusMessage = AppStrings.PresetCanceled;
            _log.Info("Bulk open/close canceled.");
            ToastService.Show(AppStrings.PresetCanceled, ToastKind.Warn);
        }
        finally
        {
            _bulkCts?.Dispose();
            _bulkCts = null;
            IsBulkRunning = false;
            _bulkClosing = false;
            _focus.SuppressFallback = false;
            // Single coalesced refresh for the whole batch.
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (AccountCard card in cards)
                    card.Refresh();
                RebuildAccounts();
                RefreshServerRows();
            });
        }
    }

    private bool IsLaunchingNow(Guid accountId)
    {
        lock (_launching)
            return _launching.Contains(accountId);
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
            ToastService.Show(AppStrings.ToastFailed(card.Model.Login, ex.Message), ToastKind.Error);
        }
        finally
        {
            // Watched handles are rooted to keep Exited alive: release ours.
            lock (_watched)
            {
                if (_watched.TryGetValue(pid, out Process? watched) && ReferenceEquals(watched, owned))
                    _watched.Remove(pid);
            }
            owned?.Dispose();
        }
        if (!exited) return;
        // Explicit fallback: never rely solely on Exited to restore icons.
        card.Model.ProcessId = null;
        card.Model.WindowHandle = IntPtr.Zero;
        if (_bulkClosing) return;
        await Dispatcher.UIThread.InvokeAsync(() =>
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
                // Paired with the rooted handle above: release it here.
                process.Dispose();
                // Dedupe: the Stop fallback may have cleared this already.
                if (card.Model.ProcessId is null && card.Model.WindowHandle == IntPtr.Zero)
                    return;
                _log.Info($"Client pid={session.ProcessId} exited.");
                _dispatcher.CancelAll();
                _focus.RemoveAccount(card.Model.Id);
                card.Model.ProcessId = null;
                card.Model.WindowHandle = IntPtr.Zero;
                // During bulk the finally pass refreshes once and stays silent.
                if (_bulkClosing) return;
                ToastService.Show(AppStrings.ToastClosed(card.Model.Login));
                _ = Dispatcher.UIThread.InvokeAsync(() =>
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
        AccountDraft? draft = await _dialogs.AskAccountAsync(
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
        await Dispatcher.UIThread.InvokeAsync(RebuildAccounts);
        ToastService.Show(AppStrings.ToastSaved(draft.Login));
    }

    [RelayCommand]
    private async Task DeleteAccountAsync(AccountCard? card)
    {
        if (card is null || SelectedServer is null) return;
        if (!await _dialogs.AskConfirmAsync(
            AppStrings.DeleteAccountTitle,
            AppStrings.DeleteAccountConfirm(card.DisplayName),
            AppStrings.Delete))
            return;
        Guid id = card.Model.Id;
        string displayName = card.DisplayName;
        SelectedServer.Model.Accounts.Remove(card.Model);
        foreach (Group group in _state.Data.Groups)
            group.AccountIds.Remove(id);
        foreach (AccountTab tab in _state.Data.Tabs)
            tab.AccountIds.Remove(id);
        foreach (Preset preset in _state.Data.Presets)
            preset.Actions.RemoveAll(a => a.AccountId == id);
        await _state.SaveAsync().ConfigureAwait(false);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            RebuildAccounts();
            RefreshServerRows();
        });
        ToastService.Show(AppStrings.ToastRemoved(displayName));
    }

    [RelayCommand]
    private async Task CopyLogin(AccountCard? card)
    {
        if (card is null) return;
        // Clipboard is readable by other apps; same exposure as in-memory
        // reveal at dispatch. Standard manager behavior, user-initiated.
        // Auto-cleared below so the secret does not linger.
        IClipboard? clipboard = GetClipboard();
        if (clipboard is null) return;
        await clipboard.SetTextAsync(card.Model.Login);
        ClearClipboardAfter(TimeSpan.FromSeconds(30), card.Model.Login);
    }

    [RelayCommand]
    private async Task CopyPassword(AccountCard? card)
    {
        if (card is null) return;
        // See CopyLogin re clipboard exposure.
        string password = _state.RevealPassword(card.Model);
        IClipboard? clipboard = GetClipboard();
        if (clipboard is null) return;
        await clipboard.SetTextAsync(password);
        ClearClipboardAfter(TimeSpan.FromSeconds(30), password);
    }

    [RelayCommand]
    private async Task TogglePasswordVisibility(AccountCard? card)
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
        await Task.Delay(TimeSpan.FromSeconds(15));
        if (card.IsPasswordRevealed && card.RevealedPassword == shown)
            card.HidePassword();
    }

    private static async void ClearClipboardAfter(TimeSpan delay, string expected)
    {
        try
        {
            await Task.Delay(delay);
            IClipboard? clipboard = GetClipboard();
            if (clipboard is null) return;
            if (await clipboard.TryGetTextAsync() == expected)
                await clipboard.ClearAsync();
        }
        catch
        {
            // Clipboard busy: leave the value; the next copy overwrites it.
        }
    }

    [RelayCommand]
    private void OpenGroupMode() => _dialogs.ShowGroupWindow();

    [RelayCommand]
    private void RefreshStatus()
    {
        RefreshServerRows();
        foreach (AccountCard card in Accounts)
            card.Refresh();
    }
}
