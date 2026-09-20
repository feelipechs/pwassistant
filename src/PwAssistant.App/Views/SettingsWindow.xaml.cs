using System.Windows;
using System.Windows.Input;
using PwAssistant.App.Resources;
using PwAssistant.App.Services;
using PwAssistant.Core.Sync;

namespace PwAssistant.App.Views;

/// <summary>
/// App-level settings (gear on the main window). Edits a draft; Save
/// persists, Close/X discards. Cycle key is press-to-record (default `),
/// free choice without warnings by explicit user decision.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppState _state;
    private readonly int _originalCycleKey;
    private readonly bool _originalNumpad;
    private readonly bool _originalShiftTap;

    private int _draftCycleKey;
    private bool _recordingCycleKey;

    public SettingsWindow(AppState state)
    {
        _state = state;
        _state.Data.FocusSettings ??= new FocusSettings();
        InitializeComponent();
        Title = Strings.Settings;
        FocusConfigLabel.Text = Strings.FocusConfig;
        CycleKeyCaption.Text = Strings.CycleKey;
        NumpadCheck.Content = Strings.NumpadSelect;
        ShiftTapCheck.Content = Strings.ShiftTapToggle;
        ArmingHintLabel.Text = Strings.ArmingHint;
        SaveButton.Content = Strings.Save;
        RecordCycleButton.Content = Strings.RecordHotkey;
        CloseButton.Content = Strings.Close;

        _originalCycleKey = _draftCycleKey = _state.Data.FocusSettings.CycleKey;
        _originalNumpad = _state.Data.FocusSettings.NumpadEnabled;
        _originalShiftTap = _state.Data.FocusSettings.ShiftTapEnabled;
        UpdateCycleLabel();
        NumpadCheck.IsChecked = _originalNumpad;
        ShiftTapCheck.IsChecked = _originalShiftTap;
    }

    public static string CycleKeyLabel(int code) => code switch
    {
        0xC0 => "`",
        0x13 => "Pause",
        0x20 => "SPACE",
        0x0D => "ENTER",
        0x09 => "TAB",
        >= 0x70 and <= 0x7B => "F" + (code - 0x70 + 1),
        >= 0x41 and <= 0x5A => ((char)code).ToString(),
        >= 0x30 and <= 0x39 => ((char)code).ToString(),
        >= 0x60 and <= 0x69 => "Num" + (code - 0x60),
        _ => $"0x{code:X}",
    };

    private void OnRecordCycle(object sender, RoutedEventArgs e)
    {
        if (_recordingCycleKey)
        {
            DisarmCycleRecorder();
            return;
        }
        _recordingCycleKey = true;
        RecordCycleButton.Content = Strings.PressKeys;
        PreviewKeyDown += OnCycleRecordKey;
        RecordCycleButton.Focus();
    }

    private void DisarmCycleRecorder()
    {
        _recordingCycleKey = false;
        PreviewKeyDown -= OnCycleRecordKey;
        RecordCycleButton.Content = Strings.RecordHotkey;
        RecordCycleButton.Focus();
    }

    private void OnCycleRecordKey(object sender, KeyEventArgs e)
    {
        if (!_recordingCycleKey) return;
        if (e.Key == Key.Escape)
        {
            DisarmCycleRecorder();
            e.Handled = true;
            return;
        }
        if (e.Key is Key.Back or Key.Delete)
        {
            _draftCycleKey = FocusSettings.DefaultCycleKey;
            UpdateCycleLabel();
            DisarmCycleRecorder();
            e.Handled = true;
            return;
        }

        Key pressed = e.Key == Key.System ? e.SystemKey : e.Key;
        int code = KeyInterop.VirtualKeyFromKey(pressed);
        if (code == 0)
        {
            e.Handled = true;
            return;
        }
        _draftCycleKey = code;
        UpdateCycleLabel();
        DisarmCycleRecorder();
        e.Handled = true;
    }

    private void UpdateCycleLabel() => CycleKeyValueLabel.Text = CycleKeyLabel(_draftCycleKey);

    private void OnSave(object sender, RoutedEventArgs e)
    {
        _state.Data.FocusSettings.CycleKey = _draftCycleKey;
        _state.Data.FocusSettings.NumpadEnabled = NumpadCheck.IsChecked == true;
        _state.Data.FocusSettings.ShiftTapEnabled = ShiftTapCheck.IsChecked == true;
        SaveAsync();
        DialogResult = true;
    }

    private async void SaveAsync()
    {
        try
        {
            await _state.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, ex.Message, Strings.Settings);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    protected override void OnClosed(EventArgs e)
    {
        if (DialogResult != true)
        {
            // Discard: restore the snapshot (never partially persisted).
            _state.Data.FocusSettings.CycleKey = _originalCycleKey;
            _state.Data.FocusSettings.NumpadEnabled = _originalNumpad;
            _state.Data.FocusSettings.ShiftTapEnabled = _originalShiftTap;
        }
        base.OnClosed(e);
    }
}
