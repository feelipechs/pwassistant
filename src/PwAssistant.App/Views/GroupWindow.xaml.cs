using System.Windows;
using PwAssistant.App.Resources;
using PwAssistant.App.ViewModels;

namespace PwAssistant.App.Views;

public partial class GroupWindow : Window
{
    public GroupViewModel ViewModel { get; }

    private readonly System.Windows.Threading.DispatcherTimer _onlinePoller;

    public GroupWindow(GroupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        LimitationLabel.Text = Strings.GroundClickLimitation;
        OnlineLabel.Text = Strings.OnlineMembers;
        MembersLabel.Text = Strings.Members;
        AddMemberButton.ToolTip = Strings.AddMember;
        MiniModeButton.Content = Strings.MiniMode;
        FormationsLabel.Text = Strings.Formations;
        SaveFormationButton.Content = Strings.SaveFormation;
        LoadFormationButton.Content = Strings.LoadFormation;
        PresetsLabel.Text = Strings.Presets;
        NewPresetButton.ToolTip = Strings.NewPreset;
        AddGroupButton.ToolTip = Strings.NewGroup;
        RenameGroupButton.ToolTip = Strings.Rename;
        DeleteGroupButton.ToolTip = Strings.Delete;
        Loaded += (_, _) => ViewModel.Initialize();
        Activated += (_, _) => ViewModel.Refresh();
        // Event-oriented auto-refresh: picks up client deaths/starts without
        // requiring a window re-activation (cheap no-op when nothing changed).
        _onlinePoller = new System.Windows.Threading.DispatcherTimer(
            TimeSpan.FromSeconds(2),
            System.Windows.Threading.DispatcherPriority.Background,
            (_, _) => ViewModel.RefreshIfOnlineChanged(),
            Dispatcher);
        _onlinePoller.Start();
        Closed += (_, _) => _onlinePoller.Stop();
    }

    private static void RefreshMainHotkeys()
    {
        if (Application.Current.MainWindow is MainWindow main)
            main.RefreshPresetHotkeys();
    }
}
