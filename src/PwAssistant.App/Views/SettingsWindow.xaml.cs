using System.Windows;
using PwAssistant.App.Resources;
using PwAssistant.App.Services;
using PwAssistant.App.ViewModels;
using PwAssistant.Core.Sync;

namespace PwAssistant.App.Views;

/// <summary>
/// App-level settings (gear on the main window). Owns the B4 focus-key
/// config; the mini-mode only arms the mode itself.
/// </summary>
public partial class SettingsWindow : Window
{
    private readonly AppState _state;

    public SettingsWindow(AppState state)
    {
        _state = state;
        InitializeComponent();
        Title = Strings.Settings;
        FocusConfigLabel.Text = Strings.FocusConfig;
        CycleKeyCaption.Text = Strings.CycleKey;
        NumpadCheck.Content = Strings.NumpadSelect;
        ShiftTapCheck.Content = Strings.ShiftTapToggle;
        ArmingHintLabel.Text = Strings.ArmingHint;
        CloseButton.Content = Strings.Close;

        _state.Data.FocusSettings ??= new FocusSettings();
        CycleKeyBox.ItemsSource = GroupViewModel.CycleKeyOptions;
        CycleKeyBox.SelectedItem = GroupViewModel.CycleKeyOptions
            .FirstOrDefault(o => o.Code == _state.Data.FocusSettings.CycleKey);
        NumpadCheck.IsChecked = _state.Data.FocusSettings.NumpadEnabled;
        ShiftTapCheck.IsChecked = _state.Data.FocusSettings.ShiftTapEnabled;

        CycleKeyBox.SelectionChanged += (_, _) => { UpdateCycleWarning(); Save(); };
        NumpadCheck.Checked += (_, _) => Save();
        NumpadCheck.Unchecked += (_, _) => Save();
        ShiftTapCheck.Checked += (_, _) => Save();
        ShiftTapCheck.Unchecked += (_, _) => Save();
        UpdateCycleWarning();
    }

    /// <summary>Warns when the cycle key is a game skill key (informed consent).</summary>
    private void UpdateCycleWarning()
    {
        bool isFunctionKey = CycleKeyBox.SelectedItem is CycleKeyOption option
            && option.Label.Length >= 2
            && option.Label[0] == 'F';
        CycleWarningLabel.Text = isFunctionKey ? Strings.GameSkillWarning : string.Empty;
    }

    private void Save()
    {
        if (CycleKeyBox.SelectedItem is CycleKeyOption option)
            _state.Data.FocusSettings.CycleKey = option.Code;
        _state.Data.FocusSettings.NumpadEnabled = NumpadCheck.IsChecked == true;
        _state.Data.FocusSettings.ShiftTapEnabled = ShiftTapCheck.IsChecked == true;
        SaveAsync();
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
}
