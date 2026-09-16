using System.Collections.ObjectModel;
using System.Windows;
using PwHelper.App.Services;
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

    public PresetEditor(AppState state, IWindowResolver resolver, Preset preset)
    {
        _state = state;
        _resolver = resolver;
        _preset = preset;
        InitializeComponent();

        var accountsById = state.Data.Servers
            .SelectMany(s => s.Accounts)
            .ToDictionary(a => a.Id);
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
