using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using MouseButton = PwAssistant.Core.Models.MouseButton;
using CommunityToolkit.Mvvm.ComponentModel;
using PwAssistant.App.Resources;
using PwAssistant.App.Services;
using PwAssistant.App.ViewModels;
using PwAssistant.Core.Execution;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.WinApi;

namespace PwAssistant.App.Views;

/// <summary>One fully editable preset row (account, type, key/position, delay).</summary>
public sealed partial class PresetActionRow : ObservableObject
{
    [ObservableProperty]
    private MemberOption? selectedAccount;

    [ObservableProperty]
    private ActionType type = ActionType.Key;

    /// <summary>Key name for Key rows; null until chosen (raw by rule).</summary>
    [ObservableProperty]
    private string? key;

    /// <summary>Click position for Click rows; null until captured (raw by rule).</summary>
    [ObservableProperty]
    private RelativePosition? position;

    [ObservableProperty]
    private MouseButton button = MouseButton.Left;

    [ObservableProperty]
    private int delayBeforeMs;

    partial void OnSelectedAccountChanged(MemberOption? value)
    {
        AccountId = value?.Account.Id ?? Guid.Empty;
        AccountName = value?.DisplayName ?? string.Empty;
    }

    public Guid AccountId { get; private set; } = Guid.Empty;
    public string AccountName { get; private set; } = string.Empty;

    public string PositionLabel => Position is null
        ? Strings.NoPositionCaptured
        : string.Format(Strings.PositionCaptured, Position.X, Position.Y);

    partial void OnPositionChanged(RelativePosition? value) => OnPropertyChanged(nameof(PositionLabel));

    public int RepeatTimes { get; set; } = 1;
    public int RepeatIntervalMs { get; set; }

    public PresetActionRow Duplicate() => new()
    {
        SelectedAccount = SelectedAccount,
        Type = Type,
        Key = Key,
        Position = Position,
        Button = Button,
        DelayBeforeMs = DelayBeforeMs,
        RepeatTimes = RepeatTimes,
        RepeatIntervalMs = RepeatIntervalMs,
    };
}

public partial class PresetEditor : Window
{
    private readonly AppState _state;
    private readonly IWindowResolver _resolver;
    private readonly Preset _preset;

    public ObservableCollection<PresetActionRow> Rows { get; } = new();
    public ObservableCollection<MemberOption> MemberAccounts { get; } = new();
    public Array ActionTypes { get; } = Enum.GetValues<ActionType>();
    public Array MouseButtons { get; } = Enum.GetValues<MouseButton>();
    public IReadOnlyList<string> AvailableKeys { get; } = KeyCodes.PresetKeys;

    public PresetEditor(AppState state, IWindowResolver resolver, Preset preset)
    {
        _state = state;
        _resolver = resolver;
        _preset = preset;
        DataContext = this;
        InitializeComponent();

        var accountsById = state.Data.Servers
            .SelectMany(s => s.Accounts)
            .ToDictionary(a => a.Id);

        IEnumerable<Account> scope = state.Data.Groups
            .FirstOrDefault(g => g.Id == preset.GroupId) is Group group
            ? group.AccountIds.Where(accountsById.ContainsKey).Select(id => accountsById[id])
            : accountsById.Values;
        var optionsById = new Dictionary<Guid, MemberOption>();
        foreach (Account account in scope)
        {
            var option = new MemberOption(account);
            MemberAccounts.Add(option);
            optionsById[account.Id] = option;
        }

        foreach (AccountAction existing in preset.Actions)
        {
            optionsById.TryGetValue(existing.AccountId, out MemberOption? option);
            Rows.Add(new PresetActionRow
            {
                SelectedAccount = option,
                Type = existing.Action.Type,
                Key = existing.Action.Key,
                Position = existing.Action.RelativePosition,
                Button = existing.Action.Button,
                DelayBeforeMs = existing.Action.DelayBeforeMs,
                RepeatTimes = existing.Action.Repeat?.Times ?? 1,
                RepeatIntervalMs = existing.Action.Repeat?.IntervalMs ?? 0,
            });
        }
        ActionList.ItemsSource = Rows;
        AddCommandButton.Content = Strings.AddCommand;
        AddClickButton.Content = Strings.AddClick;
        NameCaption.Text = Strings.PresetName;
        NameBox.Text = preset.Name;
        ModeCaption.Text = Strings.ExecutionMode;
        ModeBox.ItemsSource = Enum.GetValues<ExecutionMode>();
        ModeBox.SelectedItem = preset.ExecutionMode;
        HotkeyCaption.Text = Strings.Hotkey;
        _recordedHotkey = preset.Hotkey ?? string.Empty;
        UpdateHotkeyLabel();
        RecordHotkeyButton.Content = Strings.RecordHotkey;
        HotkeyHintLabel.Text = Strings.HotkeyHint;
    }

    private string _recordedHotkey = string.Empty;
    private bool _recordingHotkey;

    /// <summary>Press-to-record with explicit armed state and focus return.</summary>
    private void OnRecordHotkey(object sender, RoutedEventArgs e)
    {
        if (_recordingHotkey)
        {
            DisarmHotkeyRecorder();
            return;
        }
        _recordingHotkey = true;
        RecordHotkeyButton.Content = Strings.PressKeys;
        PreviewKeyDown += OnHotkeyRecordKey;
        RecordHotkeyButton.Focus();
    }

    private void DisarmHotkeyRecorder()
    {
        _recordingHotkey = false;
        PreviewKeyDown -= OnHotkeyRecordKey;
        RecordHotkeyButton.Content = Strings.RecordHotkey;
        RecordHotkeyButton.Focus();
    }

    private void OnHotkeyRecordKey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (!_recordingHotkey) return;
        if (e.Key == Key.Escape)
        {
            DisarmHotkeyRecorder();
            e.Handled = true;
            return;
        }
        if (e.Key is Key.Back or Key.Delete)
        {
            _recordedHotkey = string.Empty;
            UpdateHotkeyLabel();
            DisarmHotkeyRecorder();
            e.Handled = true;
            return;
        }

        Key pressed = e.Key == Key.System ? e.SystemKey : e.Key;
        string? keyName = MapHotkeyKey(pressed);
        var parts = new List<string>();
        ModifierKeys mods = Keyboard.Modifiers;
        if (mods.HasFlag(ModifierKeys.Control)) parts.Add("CTRL");
        if (mods.HasFlag(ModifierKeys.Shift)) parts.Add("SHIFT");
        if (mods.HasFlag(ModifierKeys.Alt)) parts.Add("ALT");
        if (mods.HasFlag(ModifierKeys.Windows)) parts.Add("WIN");
        if (keyName is null || parts.Count == 0)
        {
            Error(Strings.InvalidHotkey);
            e.Handled = true;
            return;
        }
        parts.Add(keyName);
        _recordedHotkey = string.Join("+", parts);
        UpdateHotkeyLabel();
        DisarmHotkeyRecorder();
        e.Handled = true;
    }

    private void UpdateHotkeyLabel() =>
        HotkeyValueLabel.Text = string.IsNullOrEmpty(_recordedHotkey) ? Strings.NoHotkey : _recordedHotkey;

    private static string? MapHotkeyKey(Key key)
    {
        string name = key.ToString();
        if (name is "Space") return "SPACE";
        if (name is "Enter") return "ENTER";
        if (name is "Tab") return "TAB";
        if (name.Length == 1) return name; // A-Z
        if (name.Length == 2 && name[0] == 'D' && char.IsDigit(name[1])) return name[1..]; // D0-D9
        if (name.StartsWith("NumPad", StringComparison.Ordinal) && name.Length == 7 && char.IsDigit(name[6]))
            return name[6..]; // NumPad0-9
        if (name.Length >= 2 && name[0] == 'F' && int.TryParse(name[1..], out int f) && f is >= 1 and <= 12)
            return name; // F1-F12
        return null;
    }

    public Task<RelativePosition?> CaptureClickAsync(Guid accountId)
    {
        IWindowTarget? target = _state.ResolveTarget(accountId);
        if (target is null) return Task.FromResult<RelativePosition?>(null);

        var overlay = new ClickCaptureOverlay();
        WindowFocus.BringToFront(target.WindowHandle);
        overlay.ShowDialog();
        Activate();
        if (overlay.CapturedScreenPoint is null) return Task.FromResult<RelativePosition?>(null);

        (int x, int y) = _resolver.ScreenToClientPoint(
            target.WindowHandle,
            (int)overlay.CapturedScreenPoint.Value.X,
            (int)overlay.CapturedScreenPoint.Value.Y);
        (int w, int h) = _resolver.GetClientSize(target.WindowHandle);
        return Task.FromResult<RelativePosition?>(RelativePosition.FromAbsolute(x, y, w, h));
    }

    private void OnAddCommand(object sender, RoutedEventArgs e) =>
        Rows.Add(new PresetActionRow { Type = ActionType.Key });

    private void OnAddClick(object sender, RoutedEventArgs e) =>
        Rows.Add(new PresetActionRow { Type = ActionType.Click });

    private async void OnCaptureRow(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not PresetActionRow row)
            return;
        if (row.SelectedAccount is null)
        {
            Error(Strings.RowWithoutAccount(Rows.IndexOf(row) + 1));
            return;
        }
        RelativePosition? captured = await CaptureClickAsync(row.SelectedAccount.Account.Id);
        if (captured is null)
            return;
        row.Position = captured;
    }

    private void OnDuplicateRow(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PresetActionRow row)
            Rows.Insert(Rows.IndexOf(row) + 1, row.Duplicate());
    }

    private void OnRemoveRow(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PresetActionRow row)
            Rows.Remove(row);
    }

    private void OnMoveRowUp(object sender, RoutedEventArgs e) =>
        MoveRow(sender, -1);

    private void OnMoveRowDown(object sender, RoutedEventArgs e) =>
        MoveRow(sender, +1);

    private void MoveRow(object sender, int delta)
    {
        if ((sender as FrameworkElement)?.DataContext is not PresetActionRow row)
            return;
        int from = Rows.IndexOf(row);
        int to = from + delta;
        if (from < 0 || to < 0 || to >= Rows.Count)
            return;
        Rows.Move(from, to);
    }

    private void Error(string message) => ErrorLabel.Text = message;

    private void OnSave(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            Error(Strings.PresetNameRequired);
            return;
        }
        string hotkey = _recordedHotkey.Trim();
        if (!string.IsNullOrEmpty(hotkey))
        {
            try
            {
                MainWindow.ParseHotkey(hotkey);
            }
            catch (ArgumentException)
            {
                Error(Strings.InvalidHotkey);
                return;
            }
        }

        var rebuilt = new List<AccountAction>();
        for (int i = 0; i < Rows.Count; i++)
        {
            PresetActionRow row = Rows[i];
            int line = i + 1;
            if (row.SelectedAccount is null)
            {
                Error(Strings.RowWithoutAccount(line));
                return;
            }
            if (row.DelayBeforeMs < 0)
            {
                Error(Strings.InvalidDelay);
                return;
            }
            GameAction action = row.Type == ActionType.Key
                ? new GameAction { Type = ActionType.Key, Key = row.Key, DelayBeforeMs = row.DelayBeforeMs }
                : new GameAction
                {
                    Type = ActionType.Click,
                    RelativePosition = row.Position,
                    Button = row.Button,
                    DelayBeforeMs = row.DelayBeforeMs
                };

            if (row.Type == ActionType.Key && string.IsNullOrWhiteSpace(row.Key))
            {
                Error(Strings.KeyRequired);
                return;
            }
            if (row.Type == ActionType.Click && row.Position is null)
            {
                Error(Strings.ClickPositionRequired);
                return;
            }
            if (row.RepeatTimes > 1)
                action.Repeat = new RepeatSettings(row.RepeatTimes, row.RepeatIntervalMs);

            rebuilt.Add(new AccountAction { AccountId = row.SelectedAccount.Account.Id, Action = action });
        }

        _preset.Name = name;
        _preset.Hotkey = string.IsNullOrEmpty(hotkey) ? null : hotkey;
        _preset.ExecutionMode = ModeBox.SelectedItem is ExecutionMode mode
            ? mode
            : ExecutionMode.Simultaneous;
        _preset.Actions.Clear();
        _preset.Actions.AddRange(rebuilt);
        DialogResult = true;
    }
}
