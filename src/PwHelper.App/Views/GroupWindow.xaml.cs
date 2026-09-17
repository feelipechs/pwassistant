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
        SyncCheck.Content = Strings.SyncEnabled;
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
}
