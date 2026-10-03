using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform;
using Avalonia.Threading;
using PwAssistant.Avalonia.Resources;
using PwAssistant.WinApi;

namespace PwAssistant.Avalonia.Views;

/// <summary>
/// Fullscreen transparent overlay: the next left click is captured as a
/// screen point, converted to client-area coords by the caller via
/// IWindowResolver.ScreenToClientPoint, then the overlay closes.
/// Ownerless on purpose (like WPF): on close, focus stays in the game,
/// never snaps back to the preset editor.
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
        PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            _pressSeen = true;
            e.Handled = true;
        };
        PointerReleased += OnCaptureUp;
        // Tunneling (not bubbling): survives unfocused content. The global
        // hook below is the only path that survives the game owning focus.
        AddHandler(KeyDownEvent, OnPreviewKeyDown, RoutingStrategies.Tunnel);
        Opened += OnOpened;
        Closed += OnClosed;
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        // Cover every monitor: union of Screens.All (WPF VirtualScreen).
        int minX = int.MaxValue, minY = int.MaxValue;
        int maxRight = int.MinValue, maxBottom = int.MinValue;
        foreach (Screen screen in Screens.All)
        {
            PixelRect bounds = screen.Bounds;
            minX = Math.Min(minX, bounds.X);
            minY = Math.Min(minY, bounds.Y);
            maxRight = Math.Max(maxRight, bounds.X + bounds.Width);
            maxBottom = Math.Max(maxBottom, bounds.Y + bounds.Height);
        }
        if (minX != int.MaxValue)
        {
            Position = new PixelPoint(minX, minY);
            Width = maxRight - minX;
            Height = maxBottom - minY;
        }
        // NOACTIVATE: clicks land here while the game keeps foreground AND
        // keyboard the whole time. No OnSourceInitialized in Avalonia: the
        // HWND exists once shown.
        IntPtr handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle != IntPtr.Zero)
            WindowFocus.PreventActivation(handle);
        _hook.KeyTransition += OnHookKey;
    }

    private void OnClosed(object? sender, EventArgs e) =>
        _hook.KeyTransition -= OnHookKey;

    private void OnPreviewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnHookKey(KeyTransition transition)
    {
        if (!IsVisible) return;
        if (transition.VirtualKey == VK_ESCAPE && transition.IsKeyDown)
            Dispatcher.UIThread.Invoke(Close);
    }

    private void OnCaptureUp(object? sender, PointerReleasedEventArgs e)
    {
        e.Handled = true;
        // The press that opened the capture ended here: only a fresh
        // press-then-release inside the overlay counts as the pick.
        if (!_pressSeen) return;
        PixelPoint screen = TopLevel.GetTopLevel(this)?.PointToScreen(e.GetPosition(this))
            ?? new PixelPoint((int)e.GetPosition(this).X, (int)e.GetPosition(this).Y);
        CapturedScreenPoint = new Point(screen.X, screen.Y);
        Close();
    }
}
