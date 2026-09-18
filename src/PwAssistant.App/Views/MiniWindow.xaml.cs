using System.Windows;
using System.Windows.Threading;
using PwAssistant.App.Resources;
using PwAssistant.App.ViewModels;
using PwAssistant.Core.Input;
using PwAssistant.WinApi;

namespace PwAssistant.App.Views;

/// <summary>
/// Compact always-on-top group remote (B3): preset fire buttons, Sync
/// toggle and the B4 key legend. Full config stays in <see cref="GroupWindow"/>.
/// </summary>
public partial class MiniWindow : Window
{
    private bool _collapsed;
    private readonly DispatcherTimer _activeTracker;
    private readonly IWindowResolver _resolver;

    public GroupViewModel ViewModel { get; }

    public MiniWindow(GroupViewModel viewModel, IWindowResolver resolver)
    {
        ViewModel = viewModel;
        _resolver = resolver;
        DataContext = viewModel;
        InitializeComponent();
        CollapseButton.Content = "–";
        SyncCheck.Content = Strings.SyncEnabled;
        FocusCheck.Content = Strings.FocusSwitch;
        KeysLabel.Text = Strings.FocusKeysHint;
        Loaded += (_, _) => ViewModel.Initialize();
        Activated += (_, _) => ViewModel.Refresh();
        _activeTracker = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            (_, _) => TrackActiveMember(),
            Dispatcher);
        _activeTracker.Start();
        Closed += (_, _) => _activeTracker.Stop();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
            WindowFocus.PreventActivation(handle);
    }

    /// <summary>Highlights the member owning the foreground window.</summary>
    private void TrackActiveMember()
    {
        IntPtr foreground;
        try
        {
            foreground = _resolver.GetForegroundWindow();
        }
        catch (Exception)
        {
            return;
        }
        foreach (MemberOption member in ViewModel.Members)
            member.IsActive = member.Account.WindowHandle != IntPtr.Zero
                && member.Account.WindowHandle == foreground;
    }

    private void OnCollapse(object sender, RoutedEventArgs e)
    {
        _collapsed = !_collapsed;
        ContentPanel.Visibility = _collapsed ? Visibility.Collapsed : Visibility.Visible;
        CollapseButton.Content = _collapsed ? "+" : "–";
    }
}
