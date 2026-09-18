using System.Windows;
using PwAssistant.App.Resources;
using PwAssistant.App.ViewModels;

namespace PwAssistant.App.Views;

/// <summary>
/// Compact always-on-top group remote (B3): preset fire buttons, Sync
/// toggle and the B4 key legend. Full config stays in <see cref="GroupWindow"/>.
/// </summary>
public partial class MiniWindow : Window
{
    private bool _collapsed;

    public GroupViewModel ViewModel { get; }

    public MiniWindow(GroupViewModel viewModel)
    {
        ViewModel = viewModel;
        DataContext = viewModel;
        InitializeComponent();
        CollapseButton.Content = "–";
        SyncCheck.Content = Strings.SyncEnabled;
        KeysLabel.Text = Strings.FocusKeysHint;
        Loaded += (_, _) => ViewModel.Initialize();
        Activated += (_, _) => ViewModel.Refresh();
    }

    private void OnCollapse(object sender, RoutedEventArgs e)
    {
        _collapsed = !_collapsed;
        ContentPanel.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapseButton.Content = _collapsed ? "+" : "–";
    }
}
