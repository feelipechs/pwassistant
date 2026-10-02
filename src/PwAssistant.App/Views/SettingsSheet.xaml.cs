using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PwAssistant.App.Resources;
using PwAssistant.App.Services;
using PwAssistant.Core.Sync;

namespace PwAssistant.App.Views;

/// <summary>In-window settings sheet (replaces the SettingsWindow):
/// edits a draft, Save persists, dismiss discards.</summary>
public partial class SettingsSheet : UserControl
{
    private TaskCompletionSource<bool>? _tcs;
    private AppState? _state;

    private bool _originalNumpad;
    private bool _originalShiftTap;

    public SettingsSheet()
    {
        InitializeComponent();
        FocusConfigLabel.Text = Strings.FocusConfig;
        NumpadCheck.Content = Strings.NumpadSelect;
        ShiftTapCheck.Content = Strings.ShiftTapToggle;
        VerboseCheck.Content = Strings.VerboseFireLog;
        ArmingHintLabel.Text = Strings.ArmingHint;
        SaveButton.Content = Strings.Save;
        CloseButton.Content = Strings.Close;
    }

    public Task<bool> EditAsync(AppState state)
    {
        if (_tcs is not null)
            _tcs.TrySetResult(false);
        _state = state;
        _state.Data.FocusSettings ??= new FocusSettings();
        _originalNumpad = _state.Data.FocusSettings.NumpadEnabled;
        _originalShiftTap = _state.Data.FocusSettings.ShiftTapEnabled;
        NumpadCheck.IsChecked = _originalNumpad;
        ShiftTapCheck.IsChecked = _originalShiftTap;
        // Session-only diagnostics flag: applied live, outside Save/Discard.
        VerboseCheck.IsChecked = _state.VerboseFireLog;
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

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        if (_state?.Data.FocusSettings is null) return;
        _state.Data.FocusSettings.NumpadEnabled = NumpadCheck.IsChecked == true;
        _state.Data.FocusSettings.ShiftTapEnabled = ShiftTapCheck.IsChecked == true;
        // Report saved only after the disk write confirms.
        if (await SaveAsync())
            Finish(true);
    }

    private async Task<bool> SaveAsync()
    {
        if (_state is null) return false;
        try
        {
            await _state.SaveAsync().ConfigureAwait(false);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(Window.GetWindow(this), ex.Message, Strings.Settings);
            return false;
        }
    }

    private void OnVerboseChanged(object sender, RoutedEventArgs e)
    {
        if (_state is not null)
            _state.VerboseFireLog = VerboseCheck.IsChecked == true;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Discard();

    private void Discard()
    {
        // Restore the snapshot (never partially persisted).
        if (_state?.Data.FocusSettings is not null)
        {
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
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Discard();
        }
    }
}
