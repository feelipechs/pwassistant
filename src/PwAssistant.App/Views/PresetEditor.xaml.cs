using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
    private int delayBeforeMs = 100;

    partial void OnSelectedAccountChanged(MemberOption? value)
    {
        AccountId = value?.Account.Id ?? Guid.Empty;
        AccountName = value?.DisplayName ?? string.Empty;
    }

    public Guid AccountId { get; private set; } = Guid.Empty;
    public string AccountName { get; private set; } = string.Empty;

    /// <summary>One-line label for the drag ghost.</summary>
    public string GhostText
    {
        get
        {
            string what = Type == ActionType.Key ? (Key ?? "?") : PositionLabel;
            return string.IsNullOrWhiteSpace(AccountName) ? what : $"{AccountName} · {what}";
        }
    }

    public string PositionLabel => Position is null
        ? Strings.NoPositionCaptured
        : string.Format(Strings.PositionCaptured, Position.X, Position.Y);

    /// <summary>Short coords for the capture button tooltip.</summary>
    public string PositionTooltip => Position is null
        ? Strings.NoPositionCaptured
        : $"({Position.X:F2}, {Position.Y:F2})";

    partial void OnPositionChanged(RelativePosition? value)
    {
        OnPropertyChanged(nameof(PositionLabel));
        OnPropertyChanged(nameof(PositionTooltip));
    }

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
    private readonly KeyboardHook _hook;
    private readonly SyncController _sync;
    private readonly Preset _preset;

    public ObservableCollection<PresetActionRow> Rows { get; } = new();
    public ObservableCollection<MemberOption> MemberAccounts { get; } = new();
    public Array MouseButtons { get; } = Enum.GetValues<MouseButton>();
    public IReadOnlyList<string> AvailableKeys { get; } = KeyCodes.PresetKeys;

    public PresetEditor(AppState state, IWindowResolver resolver, Preset preset, KeyboardHook hook, SyncController sync)
    {
        _state = state;
        _resolver = resolver;
        _hook = hook;
        _sync = sync;
        _preset = preset;
        DataContext = this;
        InitializeComponent();
        DialogOwner.Own(this);

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
        AddCommandButton.ToolTip = Strings.AddCommand;
        AddClickButton.ToolTip = Strings.AddClick;
        NameCaption.Text = Strings.PresetName;
        NameBox.Text = preset.Name;
        ModeCaption.Text = Strings.ExecutionMode;
        ModeBox.DisplayMemberPath = "Label";
        ModeBox.SelectedValuePath = "Value";
        ModeBox.ItemsSource = new[]
        {
            new { Value = ExecutionMode.Sequential, Label = Strings.Sequential },
            new { Value = ExecutionMode.Simultaneous, Label = Strings.Simultaneous },
        };
        ModeBox.SelectedValue = preset.ExecutionMode;
        HotkeyCaption.Text = Strings.Hotkey;
        _recordedHotkey = preset.Hotkey ?? string.Empty;
        UpdateHotkeyLabel();
        RecordHotkeyButton.Content = Strings.RecordHotkey;
        HotkeyHintLabel.Text = Strings.HotkeyHint;
        _initial = TakeSnapshot();
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
        HotkeyBox.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Ring");
        PreviewKeyDown += OnHotkeyRecordKey;
        RecordHotkeyButton.Focus();
    }

    private void DisarmHotkeyRecorder()
    {
        _recordingHotkey = false;
        PreviewKeyDown -= OnHotkeyRecordKey;
        HotkeyBox.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Input");
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

        var overlay = new ClickCaptureOverlay(_hook);
        WindowFocus.BringToFront(target.WindowHandle);
        // The pick must never leak clicks: overlay swallows, Sync suspends.
        using (_sync.Suspend())
        {
            overlay.ShowDialog();
        }
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
            DuplicateRowCore(row);
    }

    private void DuplicateRowCore(PresetActionRow row) =>
        Rows.Insert(Rows.IndexOf(row) + 1, row.Duplicate());

    private void OnRemoveRow(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is PresetActionRow row)
            RemoveRowCore(row);
    }

    private void RemoveRowCore(PresetActionRow row) => Rows.Remove(row);

    private void OnDuplicateSelected(object sender, RoutedEventArgs e)
    {
        // Group semantics: copies land as a block after the last selected
        // row, in list order (1,2 → 1,2,1c,2c) — not interleaved.
        List<PresetActionRow> targets = ActionList.SelectedItems
            .OfType<PresetActionRow>()
            .OrderBy(r => Rows.IndexOf(r))
            .ToList();
        if (targets.Count == 0) return;
        int anchor = Rows.IndexOf(targets[^1]) + 1;
        foreach (PresetActionRow row in targets)
            Rows.Insert(anchor++, row.Duplicate());
    }

    private void OnRemoveSelected(object sender, RoutedEventArgs e)
    {
        List<PresetActionRow> targets = ActionList.SelectedItems.OfType<PresetActionRow>().ToList();
        if (targets.Count == 0) return;
        if (!ConfirmDialog.Ask(
            Strings.BulkDeleteRowsTitle,
            Strings.BulkDeleteRowsMessage(targets.Count),
            Strings.Delete))
            return;
        foreach (PresetActionRow row in targets)
            RemoveRowCore(row);
    }

    private Point _dragStartPoint;
    private Border? _ghost;
    private bool _dropHandled;
    private List<PresetActionRow>? _dragSnapshot;
    private readonly DragDirectionTracker _direction = new();

    /// <summary>LiveMove flips the slot ~10% into a row (on touch).</summary>
    private const double LiveSwapFraction = 0.1;

    private void OnRowPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (IsScrollbarChrome(e.OriginalSource)) return;
        _dragStartPoint = e.GetPosition(null);
    }

    /// <summary>
    /// Drag &amp; drop reorder (sole ordering gesture). Drag starts only
    /// from passive surfaces (labels/borders/padding) so text selection in
    /// TextBox and popup interaction in ComboBox/Button keep working.
    /// </summary>
    private void OnRowPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (IsScrollbarChrome(e.OriginalSource)) return;
        if (sender is not ListBox list) return;
        Point current = e.GetPosition(null);
        if (Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;
        if (!IsDragHandle(e.OriginalSource)) return;
        ListBoxItem? item = FindRowContainer(list, e.GetPosition(list));
        if (item?.DataContext is not PresetActionRow row) return;
        BeginRowDrag(item, row);
    }

    private void BeginRowDrag(FrameworkElement source, PresetActionRow row)
    {
        _dragSnapshot = Rows.ToList();
        _ghost = DragGhost.ForText(this, row.GhostText);
        GhostLayer.Children.Add(_ghost);
        PositionGhost();
        source.GiveFeedback += OnDragFeedback;
        _dropHandled = false;
        _direction.Reset();
        try
        {
            DragDrop.DoDragDrop(source, row, DragDropEffects.Move);
        }
        finally
        {
            source.GiveFeedback -= OnDragFeedback;
            GhostLayer.Children.Remove(_ghost);
            _ghost = null;
            // Cancel/ESC/release outside: LiveMove touched Rows only —
            // restore the snapshot taken at drag start.
            if (!_dropHandled && _dragSnapshot is not null)
            {
                Rows.Clear();
                foreach (PresetActionRow r in _dragSnapshot)
                    Rows.Add(r);
            }
            _dragSnapshot = null;
            _dropHandled = false;
        }
    }

    private void OnDragFeedback(object? sender, GiveFeedbackEventArgs e)
    {
        PositionGhost();
        e.UseDefaultCursors = true;
        e.Handled = true;
    }

    private void PositionGhost(Point? anchor = null)
    {
        if (_ghost is null) return;
        Point p = anchor ?? Mouse.GetPosition(GhostLayer);
        Canvas.SetLeft(_ghost, p.X + 14);
        Canvas.SetTop(_ghost, p.Y + 14);
    }

    private void OnRowDragOver(object sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        if (sender is not ListBox list || e.Data.GetData(typeof(PresetActionRow)) is not PresetActionRow dragged)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        LiveMove(list, dragged, e);
        e.Effects = DragDropEffects.Move;
        e.Handled = true;
    }

    /// <summary>Trello-style: the row moves while hovering (group parity).</summary>
    private void LiveMove(ListBox list, PresetActionRow dragged, DragEventArgs e)
    {
        int from = Rows.IndexOf(dragged);
        if (from < 0) return;
        int to = InsertionPreview.IndexAt(list, e.GetPosition(list), out _, LiveSwapFraction, _direction.Track(e, this));
        if (to > from) to--;
        to = Math.Clamp(to, 0, Rows.Count - 1);
        if (to != from)
            Rows.Move(from, to);
    }

    private void OnRowDrop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox list) return;
        if (e.Data.GetData(typeof(PresetActionRow)) is not PresetActionRow dragged) return;
        _dropHandled = true;
        // Drop where released; the hover preview already placed it nearby.
        LiveMove(list, dragged, e);
        list.SelectedItem = dragged;
    }

    private static bool IsDragHandle(object? source) =>
        source is ListBoxItem or ListBox or TextBlock or Border or Panel;

    /// <summary>Scrollbar chrome is never a drag handle: pressing the thumb
    /// or track must scroll, never start a row drag (which would freeze).</summary>
    private static bool IsScrollbarChrome(object? source)
    {
        DependencyObject? node = source as DependencyObject;
        while (node is not null)
        {
            if (node is System.Windows.Controls.Primitives.ScrollBar
                || node is System.Windows.Controls.Primitives.Thumb
                || node is System.Windows.Controls.Primitives.Track
                || node is System.Windows.Controls.Primitives.RepeatButton)
                return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private static ListBoxItem? FindRowContainer(ListBox list, Point position)
    {
        if (list.InputHitTest(position) is not DependencyObject hit) return null;
        while (hit is not null && hit is not ListBoxItem)
            hit = VisualTreeHelper.GetParent(hit);
        return hit as ListBoxItem;
    }

    private void Error(string message) => ErrorLabel.Text = message;

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        if (!ConfirmDiscard()) return;
        _skipDirtyCheck = true;
        DialogResult = false;
        Close();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (DialogResult != true && !_skipDirtyCheck && !ConfirmDiscard())
            e.Cancel = true;
        base.OnClosing(e);
    }

    private bool _skipDirtyCheck;

    /// <summary>True when the user confirmed (or had nothing to lose).</summary>
    private bool ConfirmDiscard() =>
        !IsDirty() || ConfirmDialog.Ask(
            Strings.DiscardChangesTitle,
            Strings.DiscardChangesMessage,
            Strings.DiscardChangesConfirm);

    private sealed record RowData(
        Guid AccountId, ActionType Type, string? Key,
        double? PosX, double? PosY, MouseButton Button,
        int DelayBeforeMs, int RepeatTimes, int RepeatIntervalMs);

    private sealed record EditorSnapshot(
        string Name, ExecutionMode Mode, string Hotkey, List<RowData> Rows);

    private EditorSnapshot TakeSnapshot() => new(
        NameBox.Text.Trim(),
        ModeBox.SelectedValue is ExecutionMode mode ? mode : ExecutionMode.Simultaneous,
        _recordedHotkey.Trim(),
        Rows.Select(r => new RowData(
            r.SelectedAccount?.Account.Id ?? Guid.Empty,
            r.Type, r.Key,
            r.Position?.X, r.Position?.Y,
            r.Button, r.DelayBeforeMs, r.RepeatTimes, r.RepeatIntervalMs)).ToList());

    private EditorSnapshot? _initial;

    /// <summary>Row order counts: a drag-reorder is a change.</summary>
    private bool IsDirty()
    {
        if (_initial is null) return false;
        EditorSnapshot now = TakeSnapshot();
        return _initial.Name != now.Name
            || _initial.Mode != now.Mode
            || _initial.Hotkey != now.Hotkey
            || !_initial.Rows.SequenceEqual(now.Rows);
    }

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
        _preset.ExecutionMode = ModeBox.SelectedValue is ExecutionMode mode
            ? mode
            : ExecutionMode.Simultaneous;
        _preset.Actions.Clear();
        _preset.Actions.AddRange(rebuilt);
        DialogResult = true;
    }
}
