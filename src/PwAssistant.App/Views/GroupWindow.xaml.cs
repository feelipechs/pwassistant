using System.Windows;
using PwAssistant.App.Resources;
using PwAssistant.App.ViewModels;

namespace PwAssistant.App.Views;

public partial class GroupWindow : Window
{
    public GroupViewModel ViewModel { get; }

    public GroupWindow(GroupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        LimitationLabel.Text = Strings.GroundClickLimitation;
        OnlineLabel.Text = Strings.OnlineMembers;
        MembersLabel.Text = Strings.Members;
        AddMemberButton.Content = Strings.AddMember;
        MiniModeButton.Content = Strings.MiniMode;
        FormationsLabel.Text = Strings.Formations;
        SaveFormationButton.Content = Strings.SaveFormation;
        LoadFormationButton.Content = Strings.LoadFormation;
        FocusConfigLabel.Text = Strings.FocusConfig;
        NumpadCheck.Content = Strings.NumpadSelect;
        ShiftTapCheck.Content = Strings.ShiftTapToggle;
        PresetsLabel.Text = Strings.Presets;
        NewPresetButton.Content = Strings.NewPreset;
        Loaded += (_, _) => ViewModel.Initialize();
        Activated += (_, _) => ViewModel.Refresh();
    }

    private static void RefreshMainHotkeys()
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.RefreshPresetHotkeys();
    }
}
