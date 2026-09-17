using System.Collections.ObjectModel;
using System.Windows;
using PwHelper.App.Resources;
using PwHelper.App.Services;
using PwHelper.App.ViewModels;
using PwHelper.Core.Input;
using PwHelper.Core.Models;

namespace PwHelper.App.Views;

/// <summary>Editable row binding for one AccountAction.</summary>
public sealed class PresetActionRow
{
    public Guid AccountId { get; set; }
    public string AccountName { get; set; } = string.Empty;
    public ActionType Type { get; set; } = ActionType.Key;
    public string Key { get; set; } = "F1";
    public double RelativeX { get; set; } = 0.5;
    public double RelativeY { get; set; } = 0.5;
    public int DelayBeforeMs { get; set; }
    public int RepeatTimes { get; set; } = 1;
    public int RepeatIntervalMs { get; set; }

    public string Summary =>
        Type == ActionType.Key ? $"Key {Key}" : $"Click ({RelativeX:F2},{RelativeY:F2})";
}

public partial class PresetEditor : Window
{
    private readonly AppState _state;
    private readonly IWindowResolver _resolver;
    private readonly Preset _preset;

    public ObservableCollection<PresetActionRow> Rows { get; } = new();
    public ObservableCollection<MemberOption> MemberAccounts { get; } = new();

    private RelativePosition? _capturedPosition;

    public PresetEditor(AppState state, IWindowResolver resolver, Preset preset)
    {
        _state = state;
        _resolver = resolver;
        _preset = preset;
        InitializeComponent();

        var accountsById = state.Data.Servers
            .SelectMany(s => s.Accounts)
            .ToDictionary(a => a.Id);

        IEnumerable<Account> scope = state.Data.Groups
            .FirstOrDefault(g => g.Id == preset.GroupId) is Group group
            ? group.AccountIds.Where(accountsById.ContainsKey).Select(id => accountsById[id])
            : accountsById.Values;
        foreach (Account account in scope)
            MemberAccounts.Add(new MemberOption(account));
        NewRowAccountBox.ItemsSource = MemberAccounts;
        NewRowAccountBox.SelectedIndex = MemberAccounts.Count > 0 ? 0 : -1;
        NewRowTypeBox.ItemsSource = Enum.GetValues<ActionType>();
        NewRowTypeBox.SelectedIndex = 0;
        CaptureButton.Content = Strings.CaptureClick;
        AddRowButton.Content = Strings.Add;
        UpdateNewRowInfo();
        NewRowTypeBox.SelectionChanged += (_, _) => UpdateNewRowInfo();
        foreach (AccountAction existing in preset.Actions)
        {
            accountsById.TryGetValue(existing.AccountId, out Account? account);
            Rows.Add(new PresetActionRow
            {
                AccountId = existing.AccountId,
                AccountName = account?.Role ?? existing.AccountId.ToString(),
                Type = existing.Action.Type,
                Key = existing.Action.Key ?? "F1",
                RelativeX = existing.Action.RelativePosition?.X ?? 0.5,
                RelativeY = existing.Action.RelativePosition?.Y ?? 0.5,
                DelayBeforeMs = existing.Action.DelayBeforeMs,
                RepeatTimes = existing.Action.Repeat?.Times ?? 1,
                RepeatIntervalMs = existing.Action.Repeat?.IntervalMs ?? 0
            });
        }
        ActionList.ItemsSource = Rows;
    }

    public Task<RelativePosition?> CaptureClickAsync(Guid accountId)
    {
        IWindowTarget? target = _state.ResolveTarget(accountId);
        if (target is null) return Task.FromResult<RelativePosition?>(null);

        var overlay = new ClickCaptureOverlay();
        overlay.ShowDialog();
        if (overlay.CapturedScreenPoint is null) return Task.FromResult<RelativePosition?>(null);

        (int x, int y) = _resolver.ScreenToClientPoint(
            target.WindowHandle,
            (int)overlay.CapturedScreenPoint.Value.X,
            (int)overlay.CapturedScreenPoint.Value.Y);
        (int w, int h) = _resolver.GetClientSize(target.WindowHandle);
        return Task.FromResult<RelativePosition?>(RelativePosition.FromAbsolute(x, y, w, h));
    }

    private async void OnCapture(object sender, RoutedEventArgs e)
    {
        if (NewRowAccountBox.SelectedItem is not MemberOption selected)
            return;
        RelativePosition? captured = await CaptureClickAsync(selected.Account.Id);
        if (captured is null)
            return;
        _capturedPosition = captured;
        NewRowTypeBox.SelectedItem = ActionType.Click;
        UpdateNewRowInfo();
    }

    private void OnAddRow(object sender, RoutedEventArgs e)
    {
        if (NewRowAccountBox.SelectedItem is not MemberOption selected)
            return;
        var type = NewRowTypeBox.SelectedItem is ActionType t ? t : ActionType.Key;
        string key = NewRowKeyBox.Text.Trim();

        if (type == ActionType.Key && string.IsNullOrWhiteSpace(key))
        {
            NewRowInfoLabel.Text = Strings.KeyRequired;
            return;
        }
        if (type == ActionType.Click && _capturedPosition is null)
        {
            NewRowInfoLabel.Text = Strings.ClickPositionRequired;
            return;
        }

        Rows.Add(new PresetActionRow
        {
            AccountId = selected.Account.Id,
            AccountName = selected.DisplayName,
            Type = type,
            Key = type == ActionType.Key ? key : "F1",
            RelativeX = _capturedPosition?.X ?? 0.5,
            RelativeY = _capturedPosition?.Y ?? 0.5,
        });
        _capturedPosition = null;
        UpdateNewRowInfo();
    }

    private void UpdateNewRowInfo()
    {
        NewRowInfoLabel.Text = _capturedPosition is null
            ? Strings.NoPositionCaptured
            : string.Format(Strings.PositionCaptured, _capturedPosition.X, _capturedPosition.Y);
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        var rebuilt = new List<AccountAction>();
        foreach (PresetActionRow row in Rows)
        {
            var action = new GameAction
            {
                Type = row.Type,
                Key = row.Type == ActionType.Key ? row.Key : null,
                RelativePosition = row.Type == ActionType.Click
                    ? new RelativePosition(row.RelativeX, row.RelativeY)
                    : null,
                DelayBeforeMs = row.DelayBeforeMs,
                Repeat = row.RepeatTimes > 1
                    ? new RepeatSettings(row.RepeatTimes, row.RepeatIntervalMs)
                    : null
            };

            if (action.Validate().Any())
                return; // invalid rows block the save; user fixes them in place

            rebuilt.Add(new AccountAction { AccountId = row.AccountId, Action = action });
        }

        _preset.Actions.Clear();
        _preset.Actions.AddRange(rebuilt);
        DialogResult = true;
    }
}
