using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Threading;
using PwAssistant.Avalonia.Resources;
using PwAssistant.Avalonia.Services;
using PwAssistant.Avalonia.ViewModels;
using PwAssistant.Core.Execution;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.WinApi;

namespace PwAssistant.Avalonia.Views;

/// <summary>Online members stay visible, offline collapse (replaces the WPF
/// ListBoxItem container-trigger that hid offline rows).</summary>
public sealed class OnlineOnlyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is AccountStatus status && status == AccountStatus.Online;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Compact always-on-top group remote: preset fire buttons, Sync
/// toggle and the key legend. Full config stays in GroupWindow.
/// Focus-steal avoidance (WPF WS_EX_NOACTIVATE): ShowInTaskbar=false +
/// Topmost + PreventActivation on open, Focusable=false on every button,
/// and fire paths never call Activate() — the toggle retarget in
/// DialogService uses Activate() but NOACTIVATE makes it a silent no-op.
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
        WindowChrome.ApplyNative(this, ThemeManager.IsDark);
        DialogOwner.Own(this);
        ToolTip.SetTip(SyncCheck, Strings.SyncEnabled);
        ToolTip.SetTip(FocusCheck, Strings.FocusSwitch);
        SyncLabel.Text = Strings.SyncEnabled;
        FocusLabel.Text = Strings.FocusSwitch;
        Loaded += (_, _) =>
        {
            ViewModel.Initialize();
            // Alternar-janelas on by default (shared VM: also affects Group).
            ViewModel.FocusEnabled = true;
        };
        Opened += (_, _) =>
        {
            // No OnSourceInitialized in Avalonia: the HWND exists once shown.
            IntPtr handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero)
                WindowFocus.PreventActivation(handle);
        };
        Activated += (_, _) => ViewModel.Refresh();
        _activeTracker = new DispatcherTimer(
            TimeSpan.FromMilliseconds(250),
            DispatcherPriority.Background,
            (_, _) => OnTrackerTick());
        _activeTracker.Start();
        Closed += (_, _) => _activeTracker.Stop();
        Closed += (_, _) => SwitchModesOff();
        // No hide-cache: the Mini closes for real so DialogService.Closed
        // reshows the Group (a hidden Mini swallowed re-minimize: Activate
        // on a hidden window does nothing visible).
        // (WPF also clamped maximize via MaximizeClamp: not ported —
        // CanResize=false leaves nothing to clamp.)
        ViewModel.Loops.Changed += OnLoopsChanged;
        Closed += (_, _) => ViewModel.Loops.Changed -= OnLoopsChanged;
        ViewModel.Loops.Progressed += OnLoopProgressed;
        Closed += (_, _) => ViewModel.Loops.Progressed -= OnLoopProgressed;
    }

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        // Real close (no hide-cache): DialogService.Closed reshows the Group.
        // A hidden Mini broke re-minimize (Activate on a hidden window is a
        // no-op) and left the Group buried.
        SwitchModesOff();
        base.OnClosing(e);
    }

    private void OnLoopsChanged() =>
        Dispatcher.UIThread.Post(ViewModel.RefreshLoopStates);

    /// <summary>Loop iteration feedback (marshaled like loop states).</summary>
    private void OnLoopProgressed(Guid presetId, PresetExecutionResult result) =>
        Dispatcher.UIThread.Post(() =>
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

    private int _tickCount;

    /// <summary>
    /// Fast active-member highlight (250 ms) + slow online-set refresh (~2 s,
    /// same event-oriented rule as the group grid).
    /// </summary>
    private void OnTrackerTick()
    {
        if (!IsVisible) return;
        TrackActiveMember();
        if (++_tickCount % 8 == 0)
            _ = ViewModel.RefreshIfOnlineChangedAsync();
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
