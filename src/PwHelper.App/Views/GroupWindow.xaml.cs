using System.Windows;
using PwHelper.App.Resources;
using PwHelper.App.ViewModels;

namespace PwHelper.App.Views;

public partial class GroupWindow : Window
{
    public GroupViewModel ViewModel { get; }

    public GroupWindow(GroupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        viewModel.HotkeysChanged = RefreshMainHotkeys;
        SyncCheck.Content = Strings.SyncEnabled;
        FocusCheck.Content = Strings.FocusSwitch;
        LimitationLabel.Text = Strings.GroundClickLimitation;
        OnlineLabel.Text = Strings.OnlineMembers;
        MembersLabel.Text = Strings.Members;
        AddMemberButton.Content = Strings.AddMember;
        FormationsLabel.Text = Strings.Formations;
        SaveFormationButton.Content = Strings.SaveFormation;
        LoadFormationButton.Content = Strings.LoadFormation;
        PresetsLabel.Text = Strings.Presets;
        NewPresetButton.Content = Strings.NewPreset;
        CancelButton.Content = Strings.Cancel;
        Loaded += (_, _) => ViewModel.Initialize();
        Activated += (_, _) => ViewModel.Refresh();
    }

    private static void RefreshMainHotkeys()
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.RefreshPresetHotkeys();
    }
}
