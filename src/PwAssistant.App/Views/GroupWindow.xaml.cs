using System.Windows;
using System.Windows.Threading;
using PwAssistant.App.Resources;
using PwAssistant.App.ViewModels;

namespace PwAssistant.App.Views;

public partial class GroupWindow : Window
{
    public GroupViewModel ViewModel { get; }

    private readonly DispatcherTimer _onlinePoller;

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
        PresetsLabel.Text = Strings.Presets;
        NewPresetButton.Content = Strings.NewPreset;
        Loaded += (_, _) => ViewModel.Initialize();
        Activated += (_, _) => ViewModel.Refresh();
        // Event-oriented auto-refresh: picks up client deaths/starts without
        // requiring a window re-activation (cheap no-op when nothing changed).
        _onlinePoller = new DispatcherTimer(
            TimeSpan.FromSeconds(2),
            DispatcherPriority.Background,
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
