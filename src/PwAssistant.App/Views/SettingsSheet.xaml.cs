using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PwAssistant.App.Resources;
using PwAssistant.App.Services;
using PwAssistant.Core.Sync;

namespace PwAssistant.App.Views;

/// <summary>In-window settings sheet (replaces the SettingsWindow):
/// edits a draft, Save persists, dismiss discards. Cycle key is
/// press-to-record; the sheet ESC yields while recording.</summary>
public partial class SettingsSheet : UserControl
{
    private TaskCompletionSource<bool>? _tcs;
    private AppState? _state;

    private int _originalCycleKey;
    private bool _originalNumpad;
    private bool _originalShiftTap;

    private int _draftCycleKey;
    private bool _recordingCycleKey;

    public SettingsSheet()
    {
        InitializeComponent();
        FocusConfigLabel.Text = Strings.FocusConfig;
        CycleKeyCaption.Text = Strings.CycleKey;
        NumpadCheck.Content = Strings.NumpadSelect;
        ShiftTapCheck.Content = Strings.ShiftTapToggle;
        ArmingHintLabel.Text = Strings.ArmingHint;
        SaveButton.Content = Strings.Save;
        RecordCycleButton.Content = Strings.RecordHotkey;
        CloseButton.Content = Strings.Close;
    }

    public Task<bool> EditAsync(AppState state)
    {
        if (_tcs is not null)
            _tcs.TrySetResult(false);
        _state = state;
        _state.Data.FocusSettings ??= new FocusSettings();
        _originalCycleKey = _draftCycleKey = _state.Data.FocusSettings.CycleKey;
        _originalNumpad = _state.Data.FocusSettings.NumpadEnabled;
        _originalShiftTap = _state.Data.FocusSettings.ShiftTapEnabled;
        UpdateCycleLabel();
        NumpadCheck.IsChecked = _originalNumpad;
        ShiftTapCheck.IsChecked = _originalShiftTap;
        Visibility = Visibility.Visible;
        _tcs = new TaskCompletionSource<bool>();
        Dispatcher.InvokeAsync(() => CloseButton.Focus());
        return _tcs.Task;
    }

    private void Finish(bool saved)
    {
        Visibility = Visibility.Collapsed;
        TaskCompletionSource<bool>? tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult(saved);
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
        CycleKeyBox.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Ring");
        PreviewKeyDown += OnCycleRecordKey;
        RecordCycleButton.Focus();
    }

    private void DisarmCycleRecorder()
    {
        _recordingCycleKey = false;
        PreviewKeyDown -= OnCycleRecordKey;
        CycleKeyBox.BorderBrush = (System.Windows.Media.Brush)FindResource("Brush.Input");
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
        if (_state?.Data.FocusSettings is null) return;
        _state.Data.FocusSettings.CycleKey = _draftCycleKey;
        _state.Data.FocusSettings.NumpadEnabled = NumpadCheck.IsChecked == true;
        _state.Data.FocusSettings.ShiftTapEnabled = ShiftTapCheck.IsChecked == true;
        SaveAsync();
        Finish(true);
    }

    private async void SaveAsync()
    {
        if (_state is null) return;
        try
        {
            await _state.SaveAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, Strings.Settings);
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Discard();

    private void Discard()
    {
        // Restore the snapshot (never partially persisted).
        if (_state?.Data.FocusSettings is not null)
        {
            _state.Data.FocusSettings.CycleKey = _originalCycleKey;
            _state.Data.FocusSettings.NumpadEnabled = _originalNumpad;
            _state.Data.FocusSettings.ShiftTapEnabled = _originalShiftTap;
        }
        Finish(false);
    }

    private void OnDimmerDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Dimmer))
            Discard();
    }

    private void OnSheetKeyDown(object sender, KeyEventArgs e)
    {
        if (_recordingCycleKey) return;
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Discard();
        }
    }
}
