using System.Windows;
using PwAssistant.App.Resources;
using PwAssistant.App.ViewModels;

namespace PwAssistant.App.Views;

/// <summary>Per-group preset manager. The group is activated on open, so the
/// shared preset commands (and Mini rows) operate on it.</summary>
public partial class GroupPresetsWindow : Window
{
    public GroupViewModel ViewModel { get; }

    public GroupPresetsWindow(GroupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        Title = $"{Strings.Presets} — {viewModel.SelectedGroup?.Name}";
        PresetsLabel.Text = Strings.Presets;
        NewPresetButton.ToolTip = Strings.NewPreset;
    }
}
