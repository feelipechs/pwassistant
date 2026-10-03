using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PwAssistant.Avalonia.Services;
using PwAssistant.Avalonia.Views;
using PwAssistant.Core.Execution;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Sync;
using PwAssistant.Core.Ux;
using AppStrings = PwAssistant.Avalonia.Resources.Strings;

namespace PwAssistant.Avalonia.ViewModels;

/// <summary>One preset row for the mini-mode remote (fire + loop).</summary>
public sealed partial class MiniPresetRow : ObservableObject
{
    public Preset Preset { get; }

    public MiniPresetRow(Preset preset) => Preset = preset;

    public string Name => Preset.Name;

    [ObservableProperty]
    private bool isLooping;

    /// <summary>True while the loop is firing (pill blinks; text unchanged).</summary>
    [ObservableProperty]
    private bool isFiring;

    public string DisplayFireText => Name;
}

/// <summary>One group member row (online or offline) for management.</summary>
public sealed partial class MemberOption : ObservableObject
{
    public Account Account { get; }

    public MemberOption(Account account) => Account = account;

    [ObservableProperty]
    private bool isActive;

    /// <summary>Session-only sync opt-out (Mini checkbox); default included.</summary>
    [ObservableProperty]
    private bool isSyncIncluded = true;

    public string DisplayName => string.IsNullOrWhiteSpace(Account.Role)
        ? Account.Login
        : $"{Account.Role} ({Account.Login})";

    /// <summary>Character name only (no login), for dense pickers.</summary>
    public string CharacterName => string.IsNullOrWhiteSpace(Account.Role)
        ? Account.Login
        : Account.Role;

    public string StatusText => Account.Status == AccountStatus.Online
        ? AppStrings.Online
        : AppStrings.Offline;

    public Views.StatusKind DisplayStatus => Account.Status == AccountStatus.Online
        ? Views.StatusKind.Online
        : Views.StatusKind.Offline;

    /// <summary>Re-reads Account.Status (instances are reused across polls).</summary>
    public void RefreshStatus()
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(DisplayStatus));
    }

    public string? ClassImagePath => string.IsNullOrWhiteSpace(Account.Class)
        ? null
        : $"avares://PwAssistant.Avalonia/Resources/Classes/{Account.Class.Trim().ToLowerInvariant()}.png";
}

/// <summary>Drag payload: which account, from which group (null = pool).</summary>
public sealed record MemberDrag(Guid AccountId, Guid? SourceGroupId);

/// <summary>Sentinel for the in-flow "new group" slot (trailing item of
/// GroupCardsView; XAML picks the template by item type).</summary>
public sealed class NewGroupSlot
{
    public static NewGroupSlot Instance { get; } = new();

    private NewGroupSlot()
    {
    }
}

/// <summary>One group card: members, counts and active highlight.</summary>
public sealed partial class GroupCard : ObservableObject
{
    public Group Group { get; }

    public GroupCard(Group group) => Group = group;

    public ObservableCollection<MemberOption> Members { get; } = new();

    [ObservableProperty]
    private bool isActive;

    [ObservableProperty]
    private bool isMenuOpen;

    [ObservableProperty]
    private bool isDragOver;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private int onlineCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtitle))]
    private int presetCount;

    public int MemberCount => Members.Count;

    /// <summary>"N membros • M online • K presets".</summary>
    public string Subtitle => string.Format(
        AppStrings.GroupCardStats, MemberCount, OnlineCount, PresetCount);

    public void RefreshCounts(int presets)
    {
        PresetCount = presets;
        OnlineCount = Members.Count(m => m.Account.Status == AccountStatus.Online);
        OnPropertyChanged(nameof(MemberCount));
    }

    public void NotifyRenamed() => OnPropertyChanged(nameof(Group));
}

public sealed partial class GroupViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly PresetDispatcher _dispatcher;
    private readonly SyncController _sync;
    private readonly FocusController _focus;
    private readonly LoopController _loops;
    private readonly FileLogger _log;
    private readonly IDialogService _dialogs;
    private readonly IWindowResolver _resolver;

    /// <summary>Single-shot fire debounce: a second click with a job started
    /// less than this ago is ignored (double-click must not fork jobs).</summary>
    private const int SingleFireDebounceMs = 150;
    private readonly object _fireGate = new();
    private readonly Dictionary<Guid, DateTimeOffset> _lastSingleFire = new();

    public ObservableCollection<Group> Groups { get; } = new();
    public ObservableCollection<GroupCard> GroupCards { get; } = new();

    /// <summary>
    /// Cards plus the trailing new-group slot, composed manually: Avalonia
    /// has no CompositeCollection, so the sentinel is appended here and
    /// re-appended on every GroupCards change. XAML picks the template by
    /// item type (GroupCard vs NewGroupSlot), same as the WPF
    /// CollectionContainer version.
    /// </summary>
    public ObservableCollection<object> GroupCardsView { get; } = new();
    public ObservableCollection<MemberOption> Pool { get; } = new();
    public ObservableCollection<Account> OnlineMembers { get; } = new();
    public ObservableCollection<Preset> Presets { get; } = new();
    public ObservableCollection<MemberOption> Members { get; } = new();
    public ObservableCollection<Formation> Formations { get; } = new();
    public ObservableCollection<MiniPresetRow> MiniRows { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGroupSelected))]
    [NotifyPropertyChangedFor(nameof(MiniGroupTitle))]
    private Group? selectedGroup;

    [ObservableProperty]
    private GroupCard? activeCard;

    /// <summary>True while a drag session runs: skips poll rebuilds.</summary>
    public bool SuppressAutoRefresh { get; set; }

    [ObservableProperty]
    private bool syncEnabled;

    [ObservableProperty]
    private bool focusEnabled;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public string RemoveText => AppStrings.Remove;
    public string EditText => AppStrings.Edit;
    public string DeleteText => AppStrings.Delete;
    public string MinimizeText => AppStrings.Minimize;
    public string RenameText => AppStrings.Rename;
    public string DuplicatePresetText => AppStrings.DuplicatePreset;
    public string DuplicateGroupText => AppStrings.DuplicateGroup;
    public string PresetsText => AppStrings.Presets;
    public string SaveFormationText => AppStrings.SaveFormation;
    public string LoadFormationText => AppStrings.LoadFormation;
    public string LoopText => AppStrings.Loop;

    /// <summary>Group name for the Mini header (no select there).</summary>
    public string MiniGroupTitle => SelectedGroup?.Name ?? string.Empty;

    /// <summary>True when a group is picked (shows its manage actions).</summary>
    public bool IsGroupSelected => SelectedGroup is not null;

    /// <summary>Invoked after preset mutations so hotkeys re-register live.</summary>
    public Action? HotkeysChanged { get; set; }

    public GroupViewModel(
        AppState state, PresetDispatcher dispatcher, SyncController sync,
        FocusController focus, LoopController loops, FileLogger log,
        IDialogService dialogs, IWindowResolver resolver)
    {
        _state = state;
        _dispatcher = dispatcher;
        _sync = sync;
        _focus = focus;
        _loops = loops;
        _log = log;
        _dialogs = dialogs;
        _resolver = resolver;
        GroupCards.CollectionChanged += (_, _) => RefreshGroupCardsView();
        RefreshGroupCardsView();
    }

    /// <summary>Re-appends cards + trailing new-group slot (Avalonia has no
    /// CompositeCollection; ordering is manual: cards first, slot last).</summary>
    private void RefreshGroupCardsView()
    {
        GroupCardsView.Clear();
        foreach (GroupCard card in GroupCards)
            GroupCardsView.Add(card);
        GroupCardsView.Add(NewGroupSlot.Instance);
    }

    public LoopController Loops => _loops;

    public void Initialize()
    {
        Groups.Clear();
        foreach (Group group in _state.Data.Groups)
            Groups.Add(group);
        // Raw by rule: nothing preselected; the user picks group and formation.
        ActiveCard = null;
        Formations.Clear();
        foreach (Formation formation in _state.Data.Formations)
            Formations.Add(formation);
        _state.Data.FocusSettings ??= new FocusSettings();
        _focus.Settings = _state.Data.FocusSettings;
        RebuildAll();
        // Instant paint from cache; the scan lands right after (~100 ms).
        _ = RefreshIfOnlineChangedAsync();
    }

    partial void OnActiveCardChanged(GroupCard? value)
    {
        SelectedGroup = value?.Group;
        foreach (GroupCard card in GroupCards)
            card.IsActive = card == value;
    }

    partial void OnSelectedGroupChanged(Group? value) => Rebuild(value);

    /// <summary>
    /// Reconciles a bound option list with the model, reusing instances by
    /// account id so containers don't re-realize every poll (perf). Order
    /// follows the source; unknown ids drop out. Sync flags mirror the
    /// session exclusion set so rebuilds never reset the Mini checkboxes.
    /// </summary>
    private void SyncOptions(
        ObservableCollection<MemberOption> target, IEnumerable<Account> accounts)
    {
        var byId = target.ToDictionary(m => m.Account.Id);
        target.Clear();
        foreach (Account account in accounts)
        {
            if (byId.TryGetValue(account.Id, out MemberOption? existing))
            {
                existing.IsActive = false;
                existing.IsSyncIncluded = !_syncExcluded.Contains(account.Id);
                existing.RefreshStatus();
                target.Add(existing);
            }
            else
            {
                target.Add(new MemberOption(account)
                {
                    IsSyncIncluded = !_syncExcluded.Contains(account.Id),
                });
            }
        }
    }

    /// <summary>Session-only sync exclusion (Mini checkboxes), by account id.</summary>
    private readonly HashSet<Guid> _syncExcluded = new();

    /// <summary>Flips one account in/out of Sync replicas (Mini checkbox).</summary>
    [RelayCommand]
    private void ToggleSyncExclusion(MemberOption? option)
    {
        if (option is null) return;
        Guid id = option.Account.Id;
        if (_syncExcluded.Contains(id))
            _syncExcluded.Remove(id);
        else
            _syncExcluded.Add(id);
        bool included = !_syncExcluded.Contains(id);
        foreach (MemberOption other in GroupCards.SelectMany(c => c.Members)
            .Concat(Pool).Concat(Members).Where(m => m.Account.Id == id))
            other.IsSyncIncluded = included;
        _sync.SetSyncExcluded(_syncExcluded);
    }

    /// <summary>Rebuilds cards, pool and the active selection (Mini/sync path).</summary>
    public void RebuildAll()
    {
        // Cache-only by design (no process scan): freshness comes from the
        // background scan (RefreshOnlineStatusAsync). Keeps open/Activated fast.
        var byId = _state.Data.Servers
            .SelectMany(s => s.Accounts)
            .ToDictionary(a => a.Id);

        GroupCards.Clear();
        foreach (Group group in _state.Data.Groups)
        {
            var card = new GroupCard(group) { IsActive = false };
            SyncOptions(card.Members, group.AccountIds
                .Select(id => byId.GetValueOrDefault(id))
                .OfType<Account>());
            card.RefreshCounts(_state.Data.Presets.Count(p => p.GroupId == group.Id));
            GroupCards.Add(card);
        }

        var grouped = new HashSet<Guid>(_state.Data.Groups.SelectMany(g => g.AccountIds));
        SyncOptions(Pool, byId.Values
            .Where(a => !grouped.Contains(a.Id))
            .OrderBy(a => a.Role));

        if (ActiveCard is not null && !_state.Data.Groups.Contains(ActiveCard.Group))
            ActiveCard = null;
        if (ActiveCard is not null)
            ActiveCard = GroupCards.FirstOrDefault(c => c.Group == ActiveCard.Group);
        foreach (GroupCard card in GroupCards)
            card.IsActive = card == ActiveCard;
        Rebuild(SelectedGroup);
        _onlineSnapshot = OnlineIds();
    }

    private HashSet<Guid> _onlineSnapshot = new();

    private HashSet<Guid> OnlineIds() => _state.Data.Servers
        .SelectMany(s => s.Accounts)
        .Where(a => a.Status == AccountStatus.Online)
        .Select(a => a.Id)
        .ToHashSet();

    /// <summary>Recompute members/online/presets from cache (fast; the
    /// background scan corrects staleness right after).</summary>
    public void Refresh()
    {
        Rebuild(SelectedGroup);
        _ = RefreshIfOnlineChangedAsync();
    }

    /// <summary>
    /// Event-oriented grid refresh: re-resolves online status and rebuilds
    /// only when the visible online set changed. The process/window scan
    /// runs on the pool (never the UI thread); only the compare + rebuild
    /// stay on UI (bound collections). Reentrancy-guarded: the 2 s cadence
    /// skips while a scan is in flight. Must be called from the UI thread
    /// (ConfigureAwait(true) marshals the apply back to it).
    /// </summary>
    public async Task RefreshIfOnlineChangedAsync()
    {
        if (SuppressAutoRefresh) return;
        if (Interlocked.CompareExchange(ref _scanInflight, 1, 0) != 0) return;
        try
        {
            Stopwatch sw = Stopwatch.StartNew();
            Dictionary<Guid, (int? ProcessId, IntPtr WindowHandle)> snapshot =
                await Task.Run(() => _state.ComputeOnlineSnapshot()).ConfigureAwait(true);
            sw.Stop();
            if (sw.ElapsedMilliseconds > 50 && _state.VerboseFireLog)
                _log.Info($"  [scan] online snapshot {sw.ElapsedMilliseconds} ms");
            _state.ApplyOnlineSnapshot(snapshot);
            HashSet<Guid> live = OnlineIds();
            if (live.SetEquals(_onlineSnapshot)) return;
            // Deaths first: keep the focus hierarchy before rebuilding.
            foreach (Guid dead in _onlineSnapshot.Where(id => !live.Contains(id)).ToList())
                _focus.RemoveAccount(dead);
            RebuildAll();
        }
        catch (Exception ex)
        {
            // Timers and Activated handlers must never die from a scan.
            _log.Warn($"Background online scan failed: {ex.Message}");
        }
        finally
        {
            Interlocked.Exchange(ref _scanInflight, 0);
        }
    }

    private int _scanInflight;

    private void Rebuild(Group? value)
    {
        OnlineMembers.Clear();
        Presets.Clear();
        Members.Clear();
        if (value is null) return;

        // Cache-only (no scan): RebuildAll/Refresh kick the background scan.
        var accountsById = _state.Data.Servers
            .SelectMany(s => s.Accounts)
            .ToDictionary(a => a.Id);

        SyncOptions(Members, value.AccountIds
            .Select(id => accountsById.GetValueOrDefault(id))
            .OfType<Account>());
        OnlineMembers.Clear();
        foreach (MemberOption member in Members)
            if (member.Account.Status == AccountStatus.Online)
                OnlineMembers.Add(member.Account);

        foreach (Preset preset in _state.Data.Presets.Where(p => p.GroupId == value.Id))
            Presets.Add(preset);
        SyncMiniRows();
        RefreshLoopStates();

        _sync.SetSyncedAccounts(OnlineMembers.Select(a => a.Id));
        _focus.SetOrder(OnlineMembers.Select(a => a.Id));
    }

    partial void OnSyncEnabledChanged(bool value)
    {
        _sync.Enabled = value;
        _sync.NotifyToggled(value);
    }

    partial void OnFocusEnabledChanged(bool value) => _focus.Enabled = value;

    [RelayCommand]
    private async Task AddGroupAsync()
    {
        (bool ok, string name) = await _dialogs.AskGroupPromptAsync(this, "GroupName", string.Empty);
        if (!ok) return;
        {
            var group = new Group { Name = name };
            _state.Data.Groups.Add(group);
            Groups.Add(group);
            // No ConfigureAwait(false): RebuildAll touches UI-bound collections.
            await _state.SaveAsync();
            _log.Info($"Persisted groups: {string.Join(", ", GroupCards.Select(c => $"{c.Group.Name}={c.Group.AccountIds.Count}"))}.");
            RebuildAll();
            ActiveCard = GroupCards.FirstOrDefault(c => c.Group == group);
        }
        ToastService.Show(AppStrings.ToastAdded(name));
    }

    /// <summary>
    /// Duplicates a group shell (no members — membership is exclusive)
    /// with its presets cloned (new ids, names kept, hotkeys kept).
    /// </summary>
    [RelayCommand]
    private async Task DuplicateGroupAsync(GroupCard? card)
    {
        if (card is null) return;
        card.IsMenuOpen = false;
        Group source = card.Group;
        var copy = new Group { Name = source.Name + AppStrings.CopySuffix };
        _state.Data.Groups.Insert(_state.Data.Groups.IndexOf(source) + 1, copy);
        Groups.Insert(Groups.IndexOf(source) + 1, copy);
        foreach (Preset preset in _state.Data.Presets.Where(p => p.GroupId == source.Id).ToList())
            _state.Data.Presets.Add(ClonePreset(preset, copy.Id, nameSuffix: null, keepHotkey: true));
        // No ConfigureAwait(false): RebuildAll touches UI-bound collections.
        await _state.SaveAsync();
        RebuildAll();
        ActiveCard = GroupCards.FirstOrDefault(c => c.Group == copy);
        ToastService.Show(AppStrings.ToastAdded(copy.Name));
    }

    [RelayCommand]
    private async Task RenameGroupAsync(GroupCard? card)
    {
        if (card is null) return;
        card.IsMenuOpen = false;
        (bool ok, string name) = await _dialogs.AskGroupPromptAsync(this, "GroupName", card.Group.Name);
        if (!ok) return;
        card.Group.Name = name;
        int index = Groups.IndexOf(card.Group);
        if (index >= 0)
        {
            Groups.RemoveAt(index);
            Groups.Insert(index, card.Group);
        }
        card.NotifyRenamed();
        await _state.SaveAsync().ConfigureAwait(false);
        ToastService.Show(AppStrings.ToastSaved(name));
    }

    [RelayCommand]
    private async Task DeleteGroupAsync(GroupCard? card)
    {
        if (card is null) return;
        card.IsMenuOpen = false;
        Group group = card.Group;
        if (await _dialogs.AskGroupConfirmAsync(this,
            AppStrings.DeleteGroupTitle,
            AppStrings.DeleteGroupConfirm(group.Name),
            AppStrings.Delete) is not true)
            return;

        Group doomed = group;
        foreach (Preset preset in _state.Data.Presets.Where(p => p.GroupId == doomed.Id).ToList())
        {
            _loops.Forget(preset.Id);
            _state.Data.Presets.Remove(preset);
        }
        _state.Data.Groups.Remove(doomed);
        Groups.Remove(doomed);
        if (ActiveCard?.Group == doomed)
            ActiveCard = null;
        // No ConfigureAwait(false): RebuildAll touches UI-bound collections.
        await _state.SaveAsync();
        RebuildAll();
        ToastService.Show(AppStrings.ToastRemoved(doomed.Name));
    }

    [RelayCommand]
    private void OpenPresets(GroupCard? card)
    {
        if (card is null) return;
        card.IsMenuOpen = false;
        ActiveCard = card;
        _dialogs.ShowPresetsTab(this);
    }

    [RelayCommand]
    private void OpenFormations(GroupCard? card)
    {
        if (card is null) return;
        card.IsMenuOpen = false;
        ActiveCard = card;
        _dialogs.ShowFormationsSheet(this);
    }

    [RelayCommand]
    private void ActivateCard(GroupCard? card)
    {
        if (card is null) return;
        ActiveCard = card;
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task FirePresetAsync(Preset? preset)
    {
        if (preset is null) return;
        // Firing while armed starts the loop; firing while looping stops
        // it (toggle stays marked either way); otherwise a single shot.
        if (_loops.IsLooping(preset.Id))
        {
            _loops.Stop(preset.Id);
            RefreshLoopStates();
            StatusMessage = AppStrings.PresetCanceled;
            return;
        }
        if (_loops.IsArmed(preset.Id))
        {
            _loops.Start(preset);
            RefreshLoopStates();
            return;
        }
        // Toggle paths above keep click semantics; only single shots debounce.
        DateTimeOffset now = DateTimeOffset.UtcNow;
        bool debounced;
        lock (_fireGate)
        {
            debounced = _lastSingleFire.TryGetValue(preset.Id, out DateTimeOffset last)
                && (now - last).TotalMilliseconds < SingleFireDebounceMs;
            if (!debounced)
                _lastSingleFire[preset.Id] = now;
        }
        if (debounced)
        {
            _log.Info($"Fire preset {preset.Name} ignored (debounced).");
            return;
        }
        try
        {
            StatusMessage = "...";
            // Fresh handles: closes the stale/recycled HWND race for clicks.
            _state.RefreshOnlineStatus();
            var countdown = new Progress<int>(left => StatusMessage = left.ToString());
            var names = _state.Data.Servers
                .SelectMany(s => s.Accounts)
                .ToDictionary(a => a.Id, a => string.IsNullOrWhiteSpace(a.Role) ? a.Login : a.Role);
            var fire = new Progress<PresetProgress>(p =>
                StatusMessage = $"{p.Index}/{p.Total} ({(names.TryGetValue(p.AccountId, out string? n) ? n : "?")})"
                    + (p.Skipped ? " " + AppStrings.SkippedMark : ""));
            PresetExecutionResult result = await _dispatcher.FireAsync(
                preset, countdownSeconds: 0, countdown, fire).ConfigureAwait(false);

            int fired = result.Accounts.Count(r => !r.Skipped);
            int skipped = result.Accounts.Count(r => r.Skipped);
            StatusMessage = AppStrings.PresetFired(fired, skipped);
            var byId = _state.Data.Servers
                .SelectMany(s => s.Accounts)
                .ToDictionary(a => a.Id);
            PresetFireLog.Log(_log, _resolver, preset, result, "manual", id =>
                byId.TryGetValue(id, out Account? a)
                    ? ((string.IsNullOrWhiteSpace(a.Role) ? a.Login : a.Role), a.ProcessId)
                    : ("?", null), _state.VerboseFireLog);
        }
        catch (OperationCanceledException)
        {
            StatusMessage = AppStrings.PresetCanceled;
            _log.Info($"Fired preset {preset.Name}: canceled.");
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
            _log.Error($"Fire preset {preset.Name} failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Persists the on-screen order after a live drag (model follows UI).
    /// Callers run on the UI thread.
    /// </summary>
    public async Task PersistGroupOrderAsync()
    {
        // First card wins: a duplicated id in corrupt data can't clone a member.
        var seen = new HashSet<Guid>();
        foreach (GroupCard card in GroupCards)
            card.Group.AccountIds = card.Members
                .Select(m => m.Account.Id)
                .Where(id => seen.Add(id))
                .ToList();
        // No ConfigureAwait(false): RebuildAll touches UI-bound collections.
        await _state.SaveAsync();
        RebuildAll();
    }

    public void RefreshCardCounts()
    {
        foreach (GroupCard card in GroupCards)
            card.RefreshCounts(_state.Data.Presets.Count(p => p.GroupId == card.Group.Id));
    }

    [RelayCommand]
    private async Task SaveFormationAsync(GroupCard? card)
    {
        if (card is null || card.Group.AccountIds.Count == 0) return;
        card.IsMenuOpen = false;
        (bool ok, string name) = await _dialogs.AskGroupPromptAsync(this, "FormationName", card.Group.Name);
        if (!ok) return;
        if (_state.Data.Formations.Any(f => string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = AppStrings.DuplicateFormationName;
            return;
        }
        var formation = new Formation
        {
            Name = name,
            AccountIds = new List<Guid>(card.Group.AccountIds)
        };
        _state.Data.Formations.Add(formation);
        Formations.Add(formation);
        await _state.SaveAsync();
    }

    /// <summary>Renames a formation (duplicate names blocked).</summary>
    public async Task RenameFormationAsync(Formation formation)
    {
        (bool ok, string name) = await _dialogs.AskGroupPromptAsync(this, "FormationName", formation.Name);
        if (!ok) return;
        if (_state.Data.Formations.Any(f => f != formation
            && string.Equals(f.Name, name, StringComparison.OrdinalIgnoreCase)))
        {
            StatusMessage = AppStrings.DuplicateFormationName;
            return;
        }
        formation.Name = name;
        int index = Formations.IndexOf(formation);
        if (index >= 0)
        {
            Formations.RemoveAt(index);
            Formations.Insert(index, formation);
        }
        await _state.SaveAsync();
    }

    /// <summary>Deletes a formation after confirm.</summary>
    public async Task DeleteFormationAsync(Formation formation)
    {
        if (await _dialogs.AskGroupConfirmAsync(this,
            AppStrings.DeleteFormationTitle,
            AppStrings.DeleteFormationConfirm(formation.Name),
            AppStrings.Delete) is not true)
            return;
        _state.Data.Formations.Remove(formation);
        Formations.Remove(formation);
        await _state.SaveAsync();
    }

    /// <summary>Display names for a formation's members ("?" when gone).</summary>
    public List<string> GetFormationMemberNames(Formation formation)
    {
        var byId = _state.Data.Servers
            .SelectMany(s => s.Accounts)
            .ToDictionary(a => a.Id);
        return formation.AccountIds
            .Select(id => byId.TryGetValue(id, out Account? account)
                ? (string.IsNullOrWhiteSpace(account.Role) ? account.Login : account.Role)
                : "?")
            .ToList();
    }

    /// <summary>Applies a saved formation to the card (used by the load list).</summary>
    public async Task LoadFormationForAsync(GroupCard card, Formation formation)
    {
        var known = _state.Data.Servers
            .SelectMany(s => s.Accounts)
            .Select(a => a.Id);
        (IReadOnlyList<Guid> applied, int skipped) =
            FormationApplicator.Apply(formation, known);
        // Exclusive membership (DnD semantics): pull the applied ids out
        // of every other group instead of duplicating them across cards.
        foreach (Group other in _state.Data.Groups.Where(g => g.Id != card.Group.Id))
            other.AccountIds.RemoveAll(applied.Contains);
        card.Group.AccountIds = new List<Guid>(applied);
        ActiveCard = card;
        await _state.SaveAsync();
        RebuildAll();
        _log.Info($"Applied formation {formation.Name}: applied={applied.Count} skipped={skipped}.");
        StatusMessage = skipped == 0
            ? AppStrings.FormationApplied(formation.Name, applied.Count)
            : AppStrings.FormationAppliedSkipped(formation.Name, applied.Count, skipped);
    }

    [RelayCommand]
    private void AddPreset()
    {
        if (SelectedGroup is null)
        {
            StatusMessage = AppStrings.NoGroupSelected;
            return;
        }
        var preset = new Preset { GroupId = SelectedGroup.Id, Name = NewPresetName() };
        _dialogs.LoadPresetInTab(this, preset, isNew: true);
    }

    private string NewPresetName() =>
        string.Format(AppStrings.PresetNameNumber, Presets.Count + 1);

    [RelayCommand]
    private void EditPreset(Preset? preset)
    {
        if (preset is null) return;
        _dialogs.LoadPresetInTab(this, preset, isNew: false);
    }

    /// <summary>Commits an editor save (new presets join the collections).</summary>
    public async Task PersistPresetAsync(Preset preset, bool isNew)
    {
        if (isNew && !_state.Data.Presets.Contains(preset))
        {
            _state.Data.Presets.Add(preset);
            Presets.Add(preset);
        }
        await _state.SaveAsync();
        RefreshMiniRows();
        Refresh();
        HotkeysChanged?.Invoke();
        _state.NotifyHotkeysChanged();
        ToastService.Show(AppStrings.ToastSaved(preset.Name));
    }

    [RelayCommand]
    private async Task DeletePresetAsync(Preset? preset)
    {
        if (preset is null) return;
        string presetName = preset.Name;
        if (await _dialogs.AskGroupConfirmAsync(this,
            AppStrings.DeletePresetTitle,
            AppStrings.DeletePresetConfirm(presetName),
            AppStrings.Delete) is not true)
            return;
        DeletePresetCore(preset);
        await _state.SaveAsync();
        HotkeysChanged?.Invoke();
        _state.NotifyHotkeysChanged();
        ToastService.Show(AppStrings.ToastRemoved(presetName));
    }

    private void DeletePresetCore(Preset preset)
    {
        _loops.Forget(preset.Id);
        _state.Data.Presets.Remove(preset);
        Presets.Remove(preset);
        MiniPresetRow? row = MiniRows.FirstOrDefault(r => r.Preset == preset);
        if (row is not null)
            MiniRows.Remove(row);
    }

    [RelayCommand]
    private async Task DuplicatePresetAsync(Preset? preset)
    {
        if (preset is null) return;
        DuplicatePresetCore(preset);
        await _state.SaveAsync();
    }

    /// <summary>Deep-clones a preset into another group (hotkey optional).</summary>
    private static Preset ClonePreset(Preset source, Guid groupId, string? nameSuffix, bool keepHotkey) => new()
    {
        GroupId = groupId,
        Name = source.Name + nameSuffix,
        Hotkey = keepHotkey ? source.Hotkey : null,
        ExecutionMode = source.ExecutionMode,
        Actions = source.Actions.Select(a => new AccountAction
        {
            AccountId = a.AccountId,
            Action = new GameAction
            {
                Type = a.Action.Type,
                Key = a.Action.Key,
                RelativePosition = a.Action.RelativePosition is null
                    ? null
                    : new RelativePosition(a.Action.RelativePosition.X, a.Action.RelativePosition.Y),
                Button = a.Action.Button,
                DelayBeforeMs = a.Action.DelayBeforeMs,
                Repeat = a.Action.Repeat is null
                    ? null
                    : new RepeatSettings(a.Action.Repeat.Times, a.Action.Repeat.IntervalMs),
            },
        }).ToList(),
    };

    private void DuplicatePresetCore(Preset preset)
    {
        Preset copy = ClonePreset(preset, preset.GroupId, AppStrings.CopySuffix, keepHotkey: false);
        _state.Data.Presets.Insert(_state.Data.Presets.IndexOf(preset) + 1, copy);
        Presets.Insert(Presets.IndexOf(preset) + 1, copy);
        MiniPresetRow? neighbor = MiniRows.FirstOrDefault(r => r.Preset == preset);
        MiniRows.Insert(neighbor is null ? MiniRows.Count : MiniRows.IndexOf(neighbor) + 1, new MiniPresetRow(copy));
    }

    [RelayCommand]
    private void ToggleLoop(MiniPresetRow? row)
    {
        if (row is null) return;
        if (_loops.IsArmed(row.Preset.Id))
        {
            _loops.SetArmed(row.Preset, false);
            _loops.Stop(row.Preset.Id);
        }
        else
        {
            _loops.SetArmed(row.Preset, true);
        }
        RefreshLoopStates();
    }

    /// <summary>Syncs row toggle (armed) and blink (firing) states.</summary>
    public void RefreshLoopStates()
    {
        foreach (MiniPresetRow row in MiniRows)
        {
            row.IsLooping = _loops.IsArmed(row.Preset.Id);
            row.IsFiring = _loops.IsLooping(row.Preset.Id);
        }
    }

    /// <summary>Rebuilds MiniRows reusing instances by preset id, so a
    /// rebuild between button-down and button-up never eats the click.
    /// Skips entirely when the id set is unchanged: clearing the collection
    /// recreates item containers even for reused rows, which can cancel an
    /// in-flight Click.</summary>
    private void SyncMiniRows()
    {
        if (MiniRows.Count == Presets.Count
            && MiniRows.Select(r => r.Preset.Id).SequenceEqual(Presets.Select(p => p.Id)))
            return;
        var byId = MiniRows.ToDictionary(r => r.Preset.Id);
        MiniRows.Clear();
        foreach (Preset preset in Presets)
        {
            if (byId.TryGetValue(preset.Id, out MiniPresetRow? existing))
            {
                MiniRows.Add(existing);
            }
            else
            {
                MiniRows.Add(new MiniPresetRow(preset)
                {
                    IsLooping = _loops.IsArmed(preset.Id),
                    IsFiring = _loops.IsLooping(preset.Id),
                });
            }
        }
    }

    private void RefreshMiniRows()
    {
        SyncMiniRows();
        RefreshLoopStates();
    }

    /// <summary>Restores the Presets view order from the model (drag cancel).</summary>
    public void RevertPresetOrder()
    {
        if (SelectedGroup is null) return;
        var byId = Presets.ToDictionary(p => p.Id);
        List<Guid> order = _state.Data.Presets
            .Where(p => p.GroupId == SelectedGroup.Id)
            .Select(p => p.Id).ToList();
        Presets.Clear();
        foreach (Guid id in order)
            if (byId.TryGetValue(id, out Preset? preset))
                Presets.Add(preset);
    }

    /// <summary>
    /// Reorders a preset inside its group (filtered position). Persists the
    /// global order and mirrors the live collections when active.
    /// </summary>
    public async Task MovePresetAsync(Guid groupId, Guid presetId, int toFiltered)
    {
        List<Preset> ordered = _state.Data.Presets.Where(p => p.GroupId == groupId).ToList();
        Preset? moving = ordered.FirstOrDefault(p => p.Id == presetId);
        if (moving is null) return;
        ordered.Remove(moving);
        int to = Math.Clamp(toFiltered, 0, ordered.Count);
        Preset? anchor = to < ordered.Count ? ordered[to] : null;
        _state.Data.Presets.Remove(moving);
        if (anchor is null)
        {
            int last = _state.Data.Presets.FindLastIndex(p => p.GroupId == groupId);
            if (last < 0)
                _state.Data.Presets.Add(moving);
            else
                _state.Data.Presets.Insert(last + 1, moving);
        }
        else
        {
            int global = _state.Data.Presets.IndexOf(anchor);
            _state.Data.Presets.Insert(global < 0 ? _state.Data.Presets.Count : global, moving);
        }
        int viewFrom = Presets.IndexOf(moving);
        if (viewFrom >= 0)
        {
            Presets.RemoveAt(viewFrom);
            Presets.Insert(Math.Clamp(to, 0, Presets.Count), moving);
        }
        RefreshMiniRows();
        // No ConfigureAwait(false): callers run on the UI thread.
        await _state.SaveAsync();
    }

    public void StopAllLoops() => _loops.StopAll();

    /// <summary>
    /// Card minimize: the single Mini retargets to this group and the Group
    /// window hides (inaccessible until the Mini closes). Main stays put —
    /// background is Main's own close gesture, not the Mini flow.
    /// Closing the Mini shows the Group again.
    /// </summary>
    [RelayCommand]
    private void MinimizeCardToMini(GroupCard? card)
    {
        if (card?.Group is not Group group) return;
        _dialogs.MinimizeGroupToMini(this, group);
    }
}
