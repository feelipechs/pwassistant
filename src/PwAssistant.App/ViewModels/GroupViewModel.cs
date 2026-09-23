using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PwAssistant.App.Services;
using PwAssistant.App.Views;
using PwAssistant.Core.Execution;
using PwAssistant.Core.Models;
using PwAssistant.Core.Sync;
using PwAssistant.Core.Ux;
using AppStrings = PwAssistant.App.Resources.Strings;

namespace PwAssistant.App.ViewModels;

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

    public string? ClassImagePath => string.IsNullOrWhiteSpace(Account.Class)
        ? null
        : $"/PwAssistant.App;component/Resources/Classes/{Account.Class.Trim().ToLowerInvariant()}.png";
}

/// <summary>Drag payload: which account, from which group (null = pool).</summary>
public sealed record MemberDrag(Guid AccountId, Guid? SourceGroupId);

/// <summary>Sentinel for the in-flow "new group" slot (CompositeCollection).</summary>
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
    private readonly Func<Preset, PresetEditor> _editorFactory;
    private readonly Func<MiniWindow> _miniWindowFactory;

    public ObservableCollection<Group> Groups { get; } = new();
    public ObservableCollection<GroupCard> GroupCards { get; } = new();

    /// <summary>
    /// Cards plus the trailing new-group slot. Composed in code because a
    /// XAML CollectionContainer has no DataContext (silent empty view).
    /// </summary>
    public CompositeCollection GroupCardsView { get; }
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
    public string PresetsText => AppStrings.Presets;
    public string SaveFormationText => AppStrings.SaveFormation;
    public string LoadFormationText => AppStrings.LoadFormation;
    public string LoopText => AppStrings.Loop;

    /// <summary>Uppercase group name for the Mini header (no select there).</summary>
    public string MiniGroupTitle => SelectedGroup?.Name.ToUpperInvariant() ?? string.Empty;

    /// <summary>True when a group is picked (shows its manage actions).</summary>
    public bool IsGroupSelected => SelectedGroup is not null;

    /// <summary>Invoked after preset mutations so hotkeys re-register live.</summary>
    public Action? HotkeysChanged { get; set; }

    public GroupViewModel(
        AppState state, PresetDispatcher dispatcher, SyncController sync,
        FocusController focus, LoopController loops, FileLogger log,
        Func<Preset, PresetEditor> editorFactory,
        Func<MiniWindow> miniWindowFactory)
    {
        _state = state;
        _dispatcher = dispatcher;
        _sync = sync;
        _focus = focus;
        _loops = loops;
        _log = log;
        _editorFactory = editorFactory;
        _miniWindowFactory = miniWindowFactory;
        GroupCardsView = new CompositeCollection
        {
            new CollectionContainer { Collection = GroupCards },
            NewGroupSlot.Instance,
        };
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
    /// follows the source; unknown ids drop out.
    /// </summary>
    private static void SyncOptions(ObservableCollection<MemberOption> target, IEnumerable<Account> accounts)
    {
        var byId = target.ToDictionary(m => m.Account.Id);
        target.Clear();
        foreach (Account account in accounts)
        {
            if (byId.TryGetValue(account.Id, out MemberOption? existing))
            {
                existing.IsActive = false;
                target.Add(existing);
            }
            else
            {
                target.Add(new MemberOption(account));
            }
        }
    }

    /// <summary>Rebuilds cards, pool and the active selection (Mini/sync path).</summary>
    public void RebuildAll()
    {
        _state.RefreshOnlineStatus();
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

    /// <summary>Recompute members/online/presets (also the Activated refresh).</summary>
    public void Refresh() => Rebuild(SelectedGroup);

    /// <summary>
    /// Event-oriented grid refresh: re-resolves online status and rebuilds
    /// only when the visible online set changed (cheap to poll on a timer).
    /// Must run on the UI thread (touches bound collections).
    /// Returns true when a rebuild happened.
    /// </summary>
    public bool RefreshIfOnlineChanged()
    {
        if (SuppressAutoRefresh) return false;
        _state.RefreshOnlineStatus();
        HashSet<Guid> live = OnlineIds();
        if (live.SetEquals(_onlineSnapshot)) return false;
        // Deaths first: keep the focus hierarchy before rebuilding.
        foreach (Guid dead in _onlineSnapshot.Where(id => !live.Contains(id)).ToList())
            _focus.RemoveAccount(dead);
        RebuildAll();
        return true;
    }

    private void Rebuild(Group? value)
    {
        OnlineMembers.Clear();
        Presets.Clear();
        Members.Clear();
        if (value is null) return;

        _state.RefreshOnlineStatus();
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
        MiniRows.Clear();
        foreach (Preset preset in Presets)
            MiniRows.Add(new MiniPresetRow(preset));
        RefreshLoopStates();

        _sync.SetSyncedAccounts(OnlineMembers.Select(a => a.Id));
        _focus.SetOrder(OnlineMembers.Select(a => a.Id));
    }

    partial void OnSyncEnabledChanged(bool value) => _sync.Enabled = value;

    partial void OnFocusEnabledChanged(bool value) => _focus.Enabled = value;

    [RelayCommand]
    private async Task AddGroupAsync()
    {
        var dialog = new TextPromptDialog("GroupName", string.Empty);
        if (dialog.ShowDialog() == true)
        {
            var group = new Group { Name = dialog.Value };
            _state.Data.Groups.Add(group);
            Groups.Add(group);
            // No ConfigureAwait(false): RebuildAll touches UI-bound collections.
            await _state.SaveAsync();
            RebuildAll();
            ActiveCard = GroupCards.FirstOrDefault(c => c.Group == group);
        }
    }

    [RelayCommand]
    private async Task RenameGroupAsync(GroupCard? card)
    {
        if (card is null) return;
        card.IsMenuOpen = false;
        var dialog = new TextPromptDialog("GroupName", card.Group.Name);
        if (dialog.ShowDialog() != true) return;
        card.Group.Name = dialog.Value;
        int index = Groups.IndexOf(card.Group);
        if (index >= 0)
        {
            Groups.RemoveAt(index);
            Groups.Insert(index, card.Group);
        }
        card.NotifyRenamed();
        await _state.SaveAsync().ConfigureAwait(false);
    }

    [RelayCommand]
    private async Task DeleteGroupAsync(GroupCard? card)
    {
        if (card is null) return;
        card.IsMenuOpen = false;
        if (!ConfirmDialog.Ask(
            AppStrings.DeleteGroupTitle,
            AppStrings.DeleteGroupConfirm(card.Group.Name),
            AppStrings.Delete))
            return;

        Group doomed = card.Group;
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
    }

    [RelayCommand]
    private void OpenPresets(GroupCard? card)
    {
        if (card is null) return;
        card.IsMenuOpen = false;
        ActiveCard = card;
        new GroupPresetsWindow(this).Show();
    }

    [RelayCommand]
    private void OpenFormations(GroupCard? card)
    {
        if (card is null) return;
        card.IsMenuOpen = false;
        ActiveCard = card;
        new GroupFormationsWindow(this, card).Show();
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
        try
        {
            StatusMessage = "...";
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
            _log.Info($"Fired preset {preset.Name}: fired={fired} skipped={skipped}.");
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
        var dialog = new TextPromptDialog("FormationName", card.Group.Name);
        if (dialog.ShowDialog() != true) return;
        var formation = new Formation
        {
            Name = dialog.Value,
            AccountIds = new List<Guid>(card.Group.AccountIds)
        };
        _state.Data.Formations.Add(formation);
        Formations.Add(formation);
        await _state.SaveAsync();
    }

    /// <summary>Applies a saved formation to the card (used by the load list).</summary>
    public async Task LoadFormationForAsync(GroupCard card, Formation formation)
    {
        var known = _state.Data.Servers
            .SelectMany(s => s.Accounts)
            .Select(a => a.Id);
        (IReadOnlyList<Guid> applied, int skipped) =
            FormationApplicator.Apply(formation, known);
        card.Group.AccountIds = new List<Guid>(applied);
        ActiveCard = card;
        await _state.SaveAsync();
        RebuildAll();
        StatusMessage = skipped == 0
            ? AppStrings.FormationApplied(formation.Name, applied.Count)
            : AppStrings.FormationAppliedSkipped(formation.Name, applied.Count, skipped);
    }

    [RelayCommand]
    private async Task AddPresetAsync()
    {
        if (SelectedGroup is null) return;
        var preset = new Preset { GroupId = SelectedGroup.Id, Name = NewPresetName() };
        PresetEditor editor = _editorFactory(preset);
        if (editor.ShowDialog() != true) return;
        _state.Data.Presets.Add(preset);
        Presets.Add(preset);
        MiniRows.Add(new MiniPresetRow(preset));
        await _state.SaveAsync();
        HotkeysChanged?.Invoke();
    }

    private string NewPresetName() =>
        string.Format(AppStrings.PresetNameNumber, Presets.Count + 1);

    [RelayCommand]
    private async Task EditPresetAsync(Preset? preset)
    {
        if (preset is null) return;
        PresetEditor editor = _editorFactory(preset);
        if (editor.ShowDialog() != true) return;
        await _state.SaveAsync();
        RefreshMiniRows();
        Refresh();
        HotkeysChanged?.Invoke();
    }

    [RelayCommand]
    private async Task DeletePresetAsync(Preset? preset)
    {
        if (preset is null) return;
        if (!ConfirmDialog.Ask(
            AppStrings.DeletePresetTitle,
            AppStrings.DeletePresetConfirm(preset.Name),
            AppStrings.Delete))
            return;
        DeletePresetCore(preset);
        await _state.SaveAsync();
        HotkeysChanged?.Invoke();
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

    private void DuplicatePresetCore(Preset preset)
    {
        var copy = new Preset
        {
            GroupId = preset.GroupId,
            Name = preset.Name + AppStrings.CopySuffix,
            Hotkey = null,
            ExecutionMode = preset.ExecutionMode,
            Actions = preset.Actions.Select(a => new AccountAction
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

    private void RefreshMiniRows()
    {
        MiniRows.Clear();
        foreach (Preset existing in Presets)
            MiniRows.Add(new MiniPresetRow(existing)
            {
                IsLooping = _loops.IsArmed(existing.Id),
                IsFiring = _loops.IsLooping(existing.Id),
            });
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
        Window? groupWindow = Application.Current.Windows
            .OfType<GroupWindow>()
            .FirstOrDefault(w => ReferenceEquals(w.ViewModel, this));
        MiniWindow? existing = Application.Current.Windows.OfType<MiniWindow>().FirstOrDefault();
        if (existing is not null)
        {
            RetargetMini(existing.ViewModel, group);
            existing.Activate();
        }
        else
        {
            MiniWindow mini = _miniWindowFactory();
            mini.Loaded += (_, _) => RetargetMini(mini.ViewModel, group);
            mini.Closed += (_, _) =>
            {
                if (mini.Dispatcher.HasShutdownStarted) return;
                groupWindow?.Show();
            };
            mini.Show();
        }
        groupWindow?.Hide();
    }

    private static void RetargetMini(GroupViewModel vm, Group group) =>
        vm.ActiveCard = vm.GroupCards.FirstOrDefault(c => c.Group.Id == group.Id);
}
