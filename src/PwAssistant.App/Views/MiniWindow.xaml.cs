using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PwAssistant.App.Resources;
using PwAssistant.App.ViewModels;
using PwAssistant.Core.Execution;
using PwAssistant.Core.Input;
using PwAssistant.WinApi;

using PwAssistant.App.Services;

namespace PwAssistant.App.Views;

/// <summary>
/// Compact always-on-top group remote (B3): preset fire buttons, Sync
/// toggle and the B4 key legend. Full config stays in <see cref="GroupWindow"/>.
/// </summary>
public partial class MiniWindow : Window
{
    private readonly DispatcherTimer _activeTracker;
    private readonly IWindowResolver _resolver;

    public GroupViewModel ViewModel { get; }

    public MiniWindow(GroupViewModel viewModel, IWindowResolver resolver)
    {
        ViewModel = viewModel;
        _resolver = resolver;
        DataContext = viewModel;
        InitializeComponent();
        DialogOwner.Own(this);
        SyncCheck.ToolTip = Strings.SyncEnabled;
        FocusCheck.ToolTip = Strings.FocusSwitch;
        SyncLabel.Text = Strings.SyncEnabled;
        FocusLabel.Text = Strings.FocusSwitch;
        Loaded += (_, _) =>
        {
            ViewModel.Initialize();
            // Alternar-janelas on by default (shared VM: also affects Group).
            ViewModel.FocusEnabled = true;
        };
        Activated += (_, _) => ViewModel.Refresh();
        _activeTracker = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            (_, _) => OnTrackerTick(),
            Dispatcher);
        _activeTracker.Start();
        Closed += (_, _) => _activeTracker.Stop();
        Closed += (_, _) => SwitchModesOff();
        ViewModel.Loops.Changed += OnLoopsChanged;
        Closed += (_, _) => ViewModel.Loops.Changed -= OnLoopsChanged;
        ViewModel.Loops.Progressed += OnLoopProgressed;
        Closed += (_, _) => ViewModel.Loops.Progressed -= OnLoopProgressed;
    }

    private void OnLoopsChanged() =>
        Dispatcher.InvokeAsync(ViewModel.RefreshLoopStates);

    /// <summary>Loop iteration feedback (marshaled like loop states).</summary>
    private void OnLoopProgressed(Guid presetId, PresetExecutionResult result) =>
        Dispatcher.InvokeAsync(() =>
        {
            int fired = result.Accounts.Count(r => !r.Skipped);
            int skipped = result.Accounts.Count(r => r.Skipped);
            ViewModel.StatusMessage = Strings.PresetFired(fired, skipped);
        });

    /// <summary>Closing the remote stops its modes (nothing runs headless).</summary>
    private void SwitchModesOff()
    {
        ViewModel.SyncEnabled = false;
        ViewModel.FocusEnabled = false;
        ViewModel.StopAllLoops();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
            WindowFocus.PreventActivation(handle);
    }

    /// <summary>Manual bottom-right resize (transparent chrome grip).</summary>
    private void OnResizeGripDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
            WindowResize.BeginBottomRight(handle);
        e.Handled = true;
    }

    private int _tickCount;

    /// <summary>
    /// Fast active-member highlight (250 ms) + slow online-set refresh (~2 s,
    /// same event-oriented rule as the group grid).
    /// </summary>
    private void OnTrackerTick()
    {
        TrackActiveMember();
        if (++_tickCount % 8 == 0)
            ViewModel.RefreshIfOnlineChanged();
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
        {
            bool active = member.Account.WindowHandle != IntPtr.Zero
                && member.Account.WindowHandle == foreground;
            if (member.IsActive != active)
                member.IsActive = active;
        }
    }
}
