using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PwHelper.App.Services;
using PwHelper.App.Views;
using PwHelper.Core.Execution;
using PwHelper.Core.Models;

namespace PwHelper.App.ViewModels;

public sealed partial class GroupViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly PresetDispatcher _dispatcher;
    private readonly SyncController _sync;

    public ObservableCollection<Group> Groups { get; } = new();
    public ObservableCollection<Account> OnlineMembers { get; } = new();
    public ObservableCollection<Preset> Presets { get; } = new();

    [ObservableProperty]
    private Group? selectedGroup;

    [ObservableProperty]
    private bool syncEnabled;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public GroupViewModel(AppState state, PresetDispatcher dispatcher, SyncController sync)
    {
        _state = state;
        _dispatcher = dispatcher;
        _sync = sync;
    }

    public void Initialize()
    {
        Groups.Clear();
        foreach (Group group in _state.Data.Groups)
            Groups.Add(group);
        SelectedGroup = Groups.FirstOrDefault();
    }

    partial void OnSelectedGroupChanged(Group? value)
    {
        OnlineMembers.Clear();
        Presets.Clear();
        if (value is null) return;

        _state.RefreshOnlineStatus();
        var accountsById = _state.Data.Servers
            .SelectMany(s => s.Accounts)
            .ToDictionary(a => a.Id);

        foreach (Guid id in value.AccountIds)
        {
            if (accountsById.TryGetValue(id, out Account? account)
                && account.Status == AccountStatus.Online)
                OnlineMembers.Add(account);
        }

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
    private void CancelAll() => _dispatcher.CancelAll();
}
