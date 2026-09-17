using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PwHelper.App.Services;
using PwHelper.App.Views;
using PwHelper.Core.Execution;
using PwHelper.Core.Models;
using AppStrings = PwHelper.App.Resources.Strings;

namespace PwHelper.App.ViewModels;

/// <summary>One group member row (online or offline) for management.</summary>
public sealed class MemberOption
{
    public Account Account { get; }

    public MemberOption(Account account) => Account = account;

    public string DisplayName => string.IsNullOrWhiteSpace(Account.Role)
        ? Account.Login
        : $"{Account.Role} ({Account.Login})";

    public string StatusText => Account.Status == AccountStatus.Online
        ? AppStrings.Online
        : AppStrings.Offline;
}

public sealed partial class GroupViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly PresetDispatcher _dispatcher;
    private readonly SyncController _sync;
    private readonly Func<Preset, PresetEditor> _editorFactory;

    public ObservableCollection<Group> Groups { get; } = new();
    public ObservableCollection<Account> OnlineMembers { get; } = new();
    public ObservableCollection<Preset> Presets { get; } = new();
    public ObservableCollection<MemberOption> Members { get; } = new();
    public ObservableCollection<MemberOption> AvailableAccounts { get; } = new();
    public ObservableCollection<Formation> Formations { get; } = new();

    [ObservableProperty]
    private Formation? selectedFormation;

    [ObservableProperty]
    private MemberOption? selectedAccountToAdd;

    [ObservableProperty]
    private Group? selectedGroup;

    [ObservableProperty]
    private bool syncEnabled;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public string RemoveText => AppStrings.Remove;
    public string EditText => AppStrings.Edit;
    public string DeleteText => AppStrings.Delete;
    public string DuplicatePresetText => AppStrings.DuplicatePreset;

    public GroupViewModel(
        AppState state, PresetDispatcher dispatcher, SyncController sync,
        Func<Preset, PresetEditor> editorFactory)
    {
        _state = state;
        _dispatcher = dispatcher;
        _sync = sync;
        _editorFactory = editorFactory;
    }

    public void Initialize()
    {
        Groups.Clear();
        foreach (Group group in _state.Data.Groups)
            Groups.Add(group);
        // Raw by rule: nothing preselected; the user picks group and formation.
        SelectedGroup = null;
        Formations.Clear();
        foreach (Formation formation in _state.Data.Formations)
            Formations.Add(formation);
        SelectedFormation = null;
    }

    partial void OnSelectedGroupChanged(Group? value) => Rebuild(value);

    /// <summary>Recompute members/online/presets (also the Activated refresh).</summary>
    public void Refresh() => Rebuild(SelectedGroup);

    private void Rebuild(Group? value)
    {
        OnlineMembers.Clear();
        Presets.Clear();
        Members.Clear();
        AvailableAccounts.Clear();
        SelectedAccountToAdd = null;
        if (value is null) return;

        _state.RefreshOnlineStatus();
        var accountsById = _state.Data.Servers
            .SelectMany(s => s.Accounts)
            .ToDictionary(a => a.Id);

        foreach (Guid id in value.AccountIds)
        {
            if (!accountsById.TryGetValue(id, out Account? account))
                continue;
            Members.Add(new MemberOption(account));
            if (account.Status == AccountStatus.Online)
                OnlineMembers.Add(account);
        }

        foreach (Account account in accountsById.Values
            .Where(a => !value.AccountIds.Contains(a.Id))
            .OrderBy(a => a.Role))
            AvailableAccounts.Add(new MemberOption(account));

        foreach (Preset preset in _state.Data.Presets.Where(p => p.GroupId == value.Id))
            Presets.Add(preset);

        _sync.SetSyncedAccounts(OnlineMembers.Select(a => a.Id));
    }

    partial void OnSyncEnabledChanged(bool value) => _sync.Enabled = value;

    [RelayCommand]
    private async Task AddGroupAsync()
    {
        var dialog = new TextPromptDialog("GroupName", string.Empty);
        if (dialog.ShowDialog() == true)
        {
            var group = new Group { Name = dialog.Value };
            _state.Data.Groups.Add(group);
            Groups.Add(group);
            SelectedGroup = group;
            await _state.SaveAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task FirePresetAsync(Preset? preset)
    {
        if (preset is null) return;
        try
        {
            StatusMessage = "...";
            var countdown = new Progress<int>(left => StatusMessage = left.ToString());
            PresetExecutionResult result = await _dispatcher.FireAsync(
                preset, countdownSeconds: 3, countdown).ConfigureAwait(false);

            int fired = result.Accounts.Count(r => !r.Skipped);
            int skipped = result.Accounts.Count(r => r.Skipped);
            StatusMessage = $"fired={fired} skipped={skipped}";
        }
        catch (OperationCanceledException)
        {
            StatusMessage = "canceled";
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private async Task AddMemberAsync()
    {
        if (SelectedGroup is null || SelectedAccountToAdd is null) return;
        SelectedGroup.AccountIds.Add(SelectedAccountToAdd.Account.Id);
        // No ConfigureAwait(false): Rebuild touches UI-bound collections,
        // so the continuation must stay on the dispatcher thread.
        await _state.SaveAsync();
        Rebuild(SelectedGroup);
    }

    [RelayCommand]
    private async Task RemoveMemberAsync(MemberOption? option)
    {
        if (SelectedGroup is null || option is null) return;
        SelectedGroup.AccountIds.Remove(option.Account.Id);
        // No ConfigureAwait(false): see AddMemberAsync.
        await _state.SaveAsync();
        Rebuild(SelectedGroup);
    }

    [RelayCommand]
    private async Task SaveFormationAsync()
    {
        if (SelectedGroup is null || SelectedGroup.AccountIds.Count == 0) return;
        var dialog = new TextPromptDialog("FormationName", SelectedGroup.Name);
        if (dialog.ShowDialog() != true) return;
        var formation = new Formation
        {
            Name = dialog.Value,
            AccountIds = new List<Guid>(SelectedGroup.AccountIds)
        };
        _state.Data.Formations.Add(formation);
        Formations.Add(formation);
        SelectedFormation = formation;
        await _state.SaveAsync();
    }

    [RelayCommand]
    private async Task LoadFormationAsync()
    {
        if (SelectedGroup is null || SelectedFormation is null) return;
        var known = _state.Data.Servers
            .SelectMany(s => s.Accounts)
            .Select(a => a.Id);
        (IReadOnlyList<Guid> applied, int skipped) =
            FormationApplicator.Apply(SelectedFormation, known);
        SelectedGroup.AccountIds = new List<Guid>(applied);
        await _state.SaveAsync();
        Rebuild(SelectedGroup);
        StatusMessage = skipped == 0
            ? $"formation={SelectedFormation.Name} members={applied.Count}"
            : $"formation={SelectedFormation.Name} members={applied.Count} skipped={skipped}";
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
        await _state.SaveAsync();
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
        Refresh();
    }

    [RelayCommand]
    private async Task DeletePresetAsync(Preset? preset)
    {
        if (preset is null) return;
        _state.Data.Presets.Remove(preset);
        Presets.Remove(preset);
        await _state.SaveAsync();
    }

    [RelayCommand]
    private async Task DuplicatePresetAsync(Preset? preset)
    {
        if (preset is null) return;
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
        _state.Data.Presets.Add(copy);
        Presets.Add(copy);
        await _state.SaveAsync();
    }

    [RelayCommand]
    private void CancelAll() => _dispatcher.CancelAll();
}
