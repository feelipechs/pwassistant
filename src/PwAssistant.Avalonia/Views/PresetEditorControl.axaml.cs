using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.ComponentModel;
using PwAssistant.Avalonia.Resources;
using PwAssistant.Avalonia.Services;
using PwAssistant.Avalonia.ViewModels;
using PwAssistant.Core.Execution;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;
using MouseButton = PwAssistant.Core.Models.MouseButton;

namespace PwAssistant.Avalonia.Views;

/// <summary>One fully editable preset row (account, type, key/position, delay).</summary>
public sealed partial class PresetActionRow : ObservableObject
{
    [ObservableProperty]
    private MemberOption? selectedAccount;

    /// <summary>True when the row references an account outside the preset group scope
    /// (e.g. cloned group shell). Select stays empty on purpose; UI shows an info badge.</summary>
    [ObservableProperty]
    private bool hasMissingAccount;

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
        if (value is not null)
            HasMissingAccount = false;
    }

    partial void OnTypeChanged(ActionType value)
    {
        OnPropertyChanged(nameof(IsKeyRow));
        OnPropertyChanged(nameof(IsClickRow));
    }

    /// <summary>Row-template visibility (replaces the WPF DataTemplate.Triggers).</summary>
    public bool IsKeyRow => Type == ActionType.Key;

    /// <summary>Row-template visibility (replaces the WPF DataTemplate.Triggers).</summary>
    public bool IsClickRow => Type == ActionType.Click;

    public Guid AccountId { get; private set; } = Guid.Empty;
    public string AccountName { get; private set; } = string.Empty;

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

    /// <summary>One-line label for the drag ghost.</summary>
    public string GhostText
    {
        get
        {
            string what = Type == ActionType.Key ? (Key ?? "?") : PositionLabel;
            return string.IsNullOrWhiteSpace(AccountName) ? what : $"{AccountName} · {what}";
        }
    }

    public int RepeatTimes { get; set; } = 1;
    public int RepeatIntervalMs { get; set; }

    public PresetActionRow Duplicate() => new()
    {
        SelectedAccount = SelectedAccount,
        HasMissingAccount = HasMissingAccount && SelectedAccount is null,
        Type = Type,
        Key = Key,
        Position = Position,
        Button = Button,
        DelayBeforeMs = DelayBeforeMs,
        RepeatTimes = RepeatTimes,
        RepeatIntervalMs = RepeatIntervalMs,
    };
}

/// <summary>In-tab preset editor (migrated from the PresetEditor window):
/// always visible beside the list, empty until a preset loads. Save and
/// Cancel report to the host tab, which navigates back to the cards.</summary>
public partial class PresetEditorControl : UserControl
{
    private const string RowFormat = "application/x-pwassistant-action-row";
    private static readonly DataFormat<PresetActionRow> RowDataFormat =
        DataFormat.CreateInProcessFormat<PresetActionRow>(RowFormat);

    /// <summary>Press-to-move threshold (WPF used SystemParameters minimums).</summary>
    private const double DragThreshold = 4;

    private readonly AppState _state;
    private readonly IWindowResolver _resolver;
    private readonly KeyboardHook _hook;
    private readonly SyncController _sync;
    private readonly FileLogger _log;

    private Preset? _editing;
    private bool _isNew;

    public ObservableCollection<PresetActionRow> Rows { get; } = new();
    public ObservableCollection<MemberOption> MemberAccounts { get; } = new();
    public IReadOnlyList<string> AvailableKeys { get; } = KeyCodes.PresetKeys;

    /// <summary>Labeled mouse buttons (localized display, enum value).</summary>
    public IReadOnlyList<MouseButtonOption> MouseButtonOptions { get; } = new[]
    {
        new MouseButtonOption(MouseButton.Left, Strings.LeftButton),
        new MouseButtonOption(MouseButton.Right, Strings.RightButton),
    };

    public sealed record MouseButtonOption(MouseButton Value, string Label);

    /// <summary>Raised after a valid save (host persists and navigates).</summary>
    public event Action<Preset, bool>? Saved;

    /// <summary>Raised after cancel with a clean state (host navigates).</summary>
    public event Action? Cancelled;

    public PresetEditorControl(AppState state, IWindowResolver resolver, KeyboardHook hook, SyncController sync, FileLogger log)
    {
        _state = state;
        _resolver = resolver;
        _hook = hook;
        _sync = sync;
        _log = log;
        DataContext = this;
        InitializeComponent();
        ActionList.ItemsSource = Rows;
        ToolTip.SetTip(AddCommandButton, Strings.AddCommand);
        ToolTip.SetTip(AddClickButton, Strings.AddClick);
        NameCaption.Text = Strings.PresetName;
        HotkeyCaption.Text = Strings.Hotkey;
        RecordHotkeyButton.Content = Strings.RecordHotkey;
        HotkeyHintLabel.Text = Strings.HotkeyHint;
        EmptyHintLabel.Text = Strings.SelectPresetHint;
        ClearView();
    }

    /// <summary>Loads a preset for editing (replaces any current content).</summary>
    public void LoadPreset(Preset preset, bool isNew)
    {
        _editing = preset;
        _isNew = isNew;
        ErrorLabel.Text = string.Empty;
        MemberAccounts.Clear();
        var optionsById = new Dictionary<Guid, MemberOption>();
        foreach (Account account in ScopeAccounts(preset.GroupId))
        {
            var option = new MemberOption(account);
            MemberAccounts.Add(option);
            optionsById[account.Id] = option;
        }

        Rows.Clear();
        foreach (AccountAction existing in preset.Actions)
        {
            optionsById.TryGetValue(existing.AccountId, out MemberOption? option);
            Rows.Add(new PresetActionRow
            {
                SelectedAccount = option,
                HasMissingAccount = option is null && existing.AccountId != Guid.Empty,
                Type = existing.Action.Type,
                Key = existing.Action.Key,
                Position = existing.Action.RelativePosition,
                Button = existing.Action.Button,
                DelayBeforeMs = existing.Action.DelayBeforeMs,
                RepeatTimes = existing.Action.Repeat?.Times ?? 1,
                RepeatIntervalMs = existing.Action.Repeat?.IntervalMs ?? 0,
            });
        }
        NameBox.Text = preset.Name;
        _recordedHotkey = preset.Hotkey ?? string.Empty;
        UpdateHotkeyLabel();
        _initial = TakeSnapshot();
        EmptyState.IsVisible = false;
        ImportPresetButton.IsEnabled = true;
        ExportPresetButton.IsEnabled = true;
    }

    /// <summary>Member scope for a group (all accounts when unknown).</summary>
    private IEnumerable<Account> ScopeAccounts(Guid? groupId)
    {
        var accountsById = _state.Data.Servers
            .SelectMany(s => s.Accounts)
            .ToDictionary(a => a.Id);
        return _state.Data.Groups
            .FirstOrDefault(g => g.Id == groupId) is Group group
            ? group.AccountIds.Where(accountsById.ContainsKey).Select(id => accountsById[id])
            : accountsById.Values;
    }

    /// <summary>
    /// Adds newly-available members without touching rows: safe when the tab
    /// reshows with membership changed behind an open editor (add-only, so
    /// existing selections can never be orphaned here).
    /// </summary>
    public void RefreshMemberScope()
    {
        if (_editing is null) return;
        var known = new HashSet<Guid>(MemberAccounts.Select(o => o.Account.Id));
        foreach (Account account in ScopeAccounts(_editing.GroupId))
            if (known.Add(account.Id))
                MemberAccounts.Add(new MemberOption(account));
    }

    /// <summary>True while an unsaved-new or dirty edit is loaded.</summary>
    public bool HasUnsavedChanges() => _editing is not null && IsDirty();

    /// <summary>Reloads the current preset (or clears), discarding edits.</summary>
    public void Revert()
    {
        if (_editing is null)
        {
            ClearView();
            return;
        }
        Preset kept = _editing;
        bool isNew = _isNew;
        LoadPreset(kept, isNew);
    }

    private void ClearView()
    {
        _editing = null;
        _isNew = false;
        Rows.Clear();
        MemberAccounts.Clear();
        NameBox.Text = string.Empty;
        ErrorLabel.Text = string.Empty;
        _recordedHotkey = string.Empty;
        UpdateHotkeyLabel();
        _initial = null;
        EmptyState.IsVisible = true;
        ImportPresetButton.IsEnabled = false;
        ExportPresetButton.IsEnabled = false;
    }

    private string _recordedHotkey = string.Empty;
    private bool _recordingHotkey;

    /// <summary>True while capturing a hotkey (ESC disarms instead).</summary>
    public bool IsRecordingHotkey => _recordingHotkey;

    /// <summary>Press-to-record with explicit armed state and focus return.</summary>
    private void OnRecordHotkey(object? sender, RoutedEventArgs e)
    {
        if (_recordingHotkey)
        {
            DisarmHotkeyRecorder();
            return;
        }
        _recordingHotkey = true;
        RecordHotkeyButton.Content = Strings.PressKeys;
        if (Application.Current?.TryFindResource("Brush.Ring", out object? ringRes) == true && ringRes is IBrush ring)
            HotkeyBox.BorderBrush = ring;
        // Tunneling (WPF PreviewKeyDown): survives unfocused content.
        AddHandler(KeyDownEvent, OnHotkeyRecordKey, RoutingStrategies.Tunnel);
        RecordHotkeyButton.Focus();
    }

    private void DisarmHotkeyRecorder()
    {
        _recordingHotkey = false;
        RemoveHandler(KeyDownEvent, OnHotkeyRecordKey);
        if (Application.Current?.TryFindResource("Brush.Input", out object? inputRes) == true && inputRes is IBrush input)
            HotkeyBox.BorderBrush = input;
        RecordHotkeyButton.Content = Strings.RecordHotkey;
        ErrorLabel.Text = string.Empty;
        RecordHotkeyButton.Focus();
    }

    private void OnHotkeyRecordKey(object? sender, KeyEventArgs e)
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

        string? keyName = MapHotkeyKey(e.Key);
        var parts = new List<string>();
        KeyModifiers mods = e.KeyModifiers;
        if (mods.HasFlag(KeyModifiers.Control)) parts.Add("CTRL");
        if (mods.HasFlag(KeyModifiers.Shift)) parts.Add("SHIFT");
        if (mods.HasFlag(KeyModifiers.Alt)) parts.Add("ALT");
        if (mods.HasFlag(KeyModifiers.Meta)) parts.Add("WIN");
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

    public async Task<RelativePosition?> CaptureClickAsync(Guid accountId)
    {
        IWindowTarget? target = _state.ResolveTarget(accountId);
        if (target is null) return null;

        // Same service/window calls as WPF: WinApi KeyboardHook into the
        // overlay, BringToFront before AND after, Sync suspended during the
        // pick so it never leaks clicks, ScreenToClient conversion by the
        // shared IWindowResolver. Only the modality changed: Avalonia
        // ShowDialog needs an owner (which would yank focus back to us), so
        // the ownerless overlay is Shown and awaited via Closed instead —
        // preserving the WPF "no owner on purpose" focus behavior.
        var overlay = new ClickCaptureOverlay(_hook);
        WindowFocus.BringToFront(target.WindowHandle);
        // The pick must never leak clicks: overlay swallows, Sync suspends.
        using (_sync.Suspend())
        {
            var closed = new TaskCompletionSource<object?>();
            overlay.Closed += (_, _) => closed.TrySetResult(null);
            overlay.Show();
            await closed.Task;
        }
        if (overlay.CapturedScreenPoint is null) return null;
        // End with focus in the game (no owner yanks it back anymore).
        WindowFocus.BringToFront(target.WindowHandle);

        (int x, int y) = _resolver.ScreenToClientPoint(
            target.WindowHandle,
            (int)overlay.CapturedScreenPoint.Value.X,
            (int)overlay.CapturedScreenPoint.Value.Y);
        (int w, int h) = _resolver.GetClientSize(target.WindowHandle);
        return RelativePosition.FromAbsolute(x, y, w, h);
    }

    private void OnAddCommand(object? sender, RoutedEventArgs e) =>
        Rows.Add(new PresetActionRow { Type = ActionType.Key });

    private void OnAddClick(object? sender, RoutedEventArgs e) =>
        Rows.Add(new PresetActionRow { Type = ActionType.Click });

    private async void OnCaptureRow(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not PresetActionRow row)
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

    private void OnDuplicateRow(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is PresetActionRow row)
            DuplicateRowCore(row);
    }

    private void DuplicateRowCore(PresetActionRow row) =>
        Rows.Insert(Rows.IndexOf(row) + 1, row.Duplicate());

    private void OnRemoveRow(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is PresetActionRow row)
            RemoveRowCore(row);
    }

    private void RemoveRowCore(PresetActionRow row) => Rows.Remove(row);

    private void OnActionSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        int count = ActionList.SelectedItems?.Count ?? 0;
        BulkActions.IsVisible = count > 0;
        SelectedCountLabel.Text = Strings.SelectedCount(count);
    }

    private static readonly JsonSerializerOptions PresetJsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static string SanitizeFileName(string name)
    {
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return string.IsNullOrWhiteSpace(name) ? "preset" : name.Trim();
    }

    private async void OnImportPreset(object? sender, RoutedEventArgs e)
    {
        if (_editing is null) return;
        // Avalonia StorageProvider replaces the Win32 OpenFileDialog (same
        // JSON contract, same validation, same log-on-corrupt behavior).
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;
        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = Strings.ImportPreset,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Preset JSON") { Patterns = ["*.json"] },
                new FilePickerFileType("All files") { Patterns = ["*.*"] },
            ],
        });
        if (files.Count == 0) return;
        Preset? imported;
        try
        {
            await using Stream stream = await files[0].OpenReadAsync();
            using var reader = new StreamReader(stream);
            string json = await reader.ReadToEndAsync();
            imported = JsonSerializer.Deserialize<Preset>(json, PresetJsonOptions);
        }
        catch (Exception ex)
        {
            // Keep the friendly message on screen, keep the cause in the log.
            _log.Warn($"Preset import failed: {ex.Message}");
            imported = null;
        }
        if (imported is null || string.IsNullOrWhiteSpace(imported.Name))
        {
            Error(Strings.ImportFailed);
            return;
        }
        imported.Id = Guid.NewGuid();
        imported.GroupId = _editing.GroupId;
        imported.Hotkey = null;
        LoadPreset(imported, isNew: true);
    }

    private async void OnExportPreset(object? sender, RoutedEventArgs e)
    {
        if (_editing is null) return;
        if (HasUnsavedChanges())
        {
            Error(Strings.SaveBeforeExport);
            return;
        }
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) return;
        IStorageFile? file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = Strings.ExportPreset,
            SuggestedFileName = SanitizeFileName(_editing.Name) + ".json",
            FileTypeChoices =
            [
                new FilePickerFileType("Preset JSON") { Patterns = ["*.json"] },
            ],
        });
        if (file is null) return;
        try
        {
            string json = JsonSerializer.Serialize(_editing, PresetJsonOptions);
            string? path = file.TryGetLocalPath();
            if (path is not null)
            {
                File.WriteAllText(path, json);
            }
            else
            {
                await using Stream stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(json);
            }
        }
        catch (Exception ex)
        {
            Error(ex.Message);
        }
    }

    private void OnDuplicateSelected(object? sender, RoutedEventArgs e)
    {
        // Group semantics: copies land as a block after the last selected
        // row, in list order (1,2 → 1,2,1c,2c) — not interleaved.
        List<PresetActionRow> targets = (ActionList.SelectedItems?.OfType<PresetActionRow>()
            ?? Enumerable.Empty<PresetActionRow>())
            .OrderBy(r => Rows.IndexOf(r))
            .ToList();
        if (targets.Count == 0) return;
        int anchor = Rows.IndexOf(targets[^1]) + 1;
        foreach (PresetActionRow row in targets)
            Rows.Insert(anchor++, row.Duplicate());
    }

    private async void OnRemoveSelected(object? sender, RoutedEventArgs e)
    {
        List<PresetActionRow> targets = (ActionList.SelectedItems?.OfType<PresetActionRow>()
            ?? Enumerable.Empty<PresetActionRow>()).ToList();
        if (targets.Count == 0) return;
        if (!await ConfirmSheetHost.AskAsync(
            Strings.BulkDeleteRowsTitle,
            Strings.BulkDeleteRowsMessage(targets.Count),
            Strings.Delete))
            return;
        foreach (PresetActionRow row in targets)
            RemoveRowCore(row);
    }

    private Point _pressPoint;
    private PointerPressedEventArgs? _pressTrigger;
    private bool _pressArmed;
    private bool _dragging;
    private Border? _ghost;
    private bool _dropHandled;
    private List<PresetActionRow>? _dragSnapshot;
    private readonly DragDirectionTracker _direction = new();

    /// <summary>LiveMove flips the slot ~10% into a row (on touch).</summary>
    private const double LiveSwapFraction = 0.1;

    private void OnRowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not ListBox) return;
        if (IsScrollbarChrome(e.Source)) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressPoint = e.GetPosition(this);
        _pressTrigger = e;
        _pressArmed = true;
    }

    /// <summary>
    /// Drag &amp; drop reorder (sole ordering gesture). Drag starts only
    /// from passive surfaces (labels/borders/padding) so text selection in
    /// TextBox and popup interaction in ComboBox/Button keep working.
    /// </summary>
    private async void OnRowPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_pressArmed || _dragging) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _pressArmed = false;
            return;
        }
        if (IsScrollbarChrome(e.Source)) return;
        if (sender is not ListBox list) return;
        Point current = e.GetPosition(this);
        if (Math.Abs(current.X - _pressPoint.X) < DragThreshold &&
            Math.Abs(current.Y - _pressPoint.Y) < DragThreshold)
            return;
        if (FindRowItem(e.Source) is not ListBoxItem item) return;
        if (item.DataContext is not PresetActionRow row) return;
        if (_pressTrigger is null) return;
        _pressArmed = false;
        await BeginRowDrag(item, _pressTrigger, row);
    }

    private void OnRowPointerReleased(object? sender, PointerReleasedEventArgs e) =>
        _pressArmed = false;

    private async Task BeginRowDrag(Control source, PointerPressedEventArgs trigger, PresetActionRow row)
    {
        _ghost = DragGhost.ForText(this, row.GhostText);
        GhostLayer.Children.Add(_ghost);
        PositionGhost(trigger.GetPosition(GhostLayer));
        _dropHandled = false;
        _direction.Reset();
        _dragSnapshot = Rows.ToList();
        _dragging = true;
        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(RowDataFormat, row));
        try
        {
            // Async (no modal loop): DragOver keeps positioning the ghost live.
            await DragDrop.DoDragDropAsync(trigger, data, DragDropEffects.Move);
        }
        finally
        {
            _dragging = false;
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
            _pressTrigger = null;
        }
    }

    private void PositionGhost(Point anchor)
    {
        if (_ghost is null) return;
        Canvas.SetLeft(_ghost, anchor.X + 14);
        Canvas.SetTop(_ghost, anchor.Y + 14);
    }

    private void OnRowDragOver(object? sender, DragEventArgs e)
    {
        PositionGhost(e.GetPosition(GhostLayer));
        if (sender is not ListBox list || e.DataTransfer.TryGetValue(RowDataFormat) is not PresetActionRow dragged)
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }
        LiveMove(list, dragged, e);
        e.DragEffects = DragDropEffects.Move;
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

    private void OnRowDrop(object? sender, DragEventArgs e)
    {
        if (sender is not ListBox list) return;
        if (e.DataTransfer.TryGetValue(RowDataFormat) is not PresetActionRow dragged) return;
        _dropHandled = true;
        // Drop where released; the hover preview already placed it nearby.
        LiveMove(list, dragged, e);
        list.SelectedItem = dragged;
    }

    /// <summary>
    /// Drag starts only from passive surfaces: walks the visual chain up to
    /// the row container and rejects interactive controls (text selection in
    /// TextBox, popups in ComboBox/Button, CheckBox toggles keep working).
    /// Returns the row container, or null when the press is not a handle.
    /// </summary>
    private static ListBoxItem? FindRowItem(object? source)
    {
        Visual? node = source as Visual;
        while (node is not null)
        {
            if (node is TextBox or ComboBox or Button or CheckBox or ScrollBar or Thumb)
                return null;
            if (node is ListBoxItem item)
                return item;
            node = node.GetVisualParent();
        }
        return null;
    }

    /// <summary>Scrollbar chrome is never a drag handle: pressing the thumb
    /// or track must scroll, never start a row drag (which would freeze).</summary>
    private static bool IsScrollbarChrome(object? source)
    {
        Visual? node = source as Visual;
        while (node is not null)
        {
            if (node is ScrollBar or Thumb)
                return true;
            node = node.GetVisualParent();
        }
        return false;
    }

    private void Error(string message) => ErrorLabel.Text = message;

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (_editing is null) return;
        ErrorLabel.Text = string.Empty;
        string name = NameBox.Text?.Trim() ?? string.Empty;
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

        _editing.Name = name;
        _editing.Hotkey = string.IsNullOrEmpty(hotkey) ? null : hotkey;
        _editing.ExecutionMode = ExecutionMode.Sequential;
        _editing.Actions.Clear();
        _editing.Actions.AddRange(rebuilt);
        // Saved state is the new clean baseline (else every later
        // navigation asks to discard).
        _initial = TakeSnapshot();
        Saved?.Invoke(_editing, _isNew);
    }

    private async void OnCancel(object? sender, RoutedEventArgs e)
    {
        if (!await ConfirmDiscardAsync()) return;
        Revert();
        Cancelled?.Invoke();
    }

    /// <summary>True when the user confirmed (or had nothing to lose).</summary>
    public Task<bool> ConfirmDiscardAsync() =>
        !IsDirty()
            ? Task.FromResult(true)
            : ConfirmSheetHost.AskAsync(
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
        NameBox.Text?.Trim() ?? string.Empty,
        ExecutionMode.Sequential,
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
}
