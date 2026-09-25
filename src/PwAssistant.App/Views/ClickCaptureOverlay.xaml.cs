using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using PwAssistant.App.Resources;
using PwAssistant.WinApi;

namespace PwAssistant.App.Views;

/// <summary>
/// Fullscreen transparent overlay: the next left click is captured as a
/// screen point, converted to client-area coords by the caller via
/// ScreenToClient, then the overlay closes.
/// </summary>
public partial class ClickCaptureOverlay : Window
{
    private const int VK_ESCAPE = 0x1B;

    private readonly KeyboardHook _hook;

    /// <summary>True once a fresh press landed inside the overlay.</summary>
    private bool _pressSeen;

    public Point? CapturedScreenPoint { get; private set; }

    public ClickCaptureOverlay(KeyboardHook hook)
    {
        _hook = hook;
        InitializeComponent();
        // No owner on purpose: on close, focus must stay in the game,
        // never snap back to the preset editor.
        HintLabel.Text = Strings.ClickOverlayHint;
        // Swallow the press; capture on release so the matching button-up
        // can never leak through to the game after Close().
        PreviewMouseLeftButtonDown += (_, e) =>
        {
            _pressSeen = true;
            e.Handled = true;
        };
        PreviewMouseLeftButtonUp += OnCaptureUp;
        // Tunneling (not bubbling): survives unfocused content. The global
        // hook below is the only path that survives the game owning focus.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        };
        Loaded += (_, _) =>
        {
            // Cover every monitor: Maximized alone only covers the primary.
            WindowState = WindowState.Normal;
            Left = SystemParameters.VirtualScreenLeft;
            Top = SystemParameters.VirtualScreenTop;
            Width = SystemParameters.VirtualScreenWidth;
            Height = SystemParameters.VirtualScreenHeight;
            // NOACTIVATE: Activate/Focus silently fail by design — the game
            // keeps foreground AND keyboard the whole time.
            _hook.KeyTransition += OnHookKey;
        };
        Closed += (_, _) => _hook.KeyTransition -= OnHookKey;
    }

    /// <summary>Never steal activation (Mini pattern): clicks land here
    /// while the game keeps foreground and keyboard the whole time.</summary>
    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        IntPtr handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
            WindowFocus.PreventActivation(handle);
    }

    private void OnHookKey(KeyTransition transition)
    {
        if (!IsVisible) return;
        if (transition.VirtualKey == VK_ESCAPE && transition.IsKeyDown)
            Dispatcher.Invoke(Close);
    }

    private void OnCaptureUp(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        // The press that opened the capture ended here: only a fresh
        // press-then-release inside the overlay counts as the pick.
        if (!_pressSeen) return;
        CapturedScreenPoint = PointToScreen(e.GetPosition(this));
        Close();
    }
}
