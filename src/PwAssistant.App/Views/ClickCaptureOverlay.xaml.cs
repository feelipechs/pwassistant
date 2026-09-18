using System.Windows;
using System.Windows.Input;
using PwAssistant.App.Resources;

namespace PwAssistant.App.Views;

/// <summary>
/// Fullscreen transparent overlay: the next left click is captured as a
/// screen point, converted to client-area coords by the caller via
/// ScreenToClient, then the overlay closes.
/// </summary>
public partial class ClickCaptureOverlay : Window
{
    public Point? CapturedScreenPoint { get; private set; }

    public ClickCaptureOverlay()
    {
        InitializeComponent();
        HintLabel.Text = Strings.ClickOverlayHint;
        MouseLeftButtonDown += OnClick;
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
                Close();
        };
    }

    private void OnClick(object sender, MouseButtonEventArgs e)
    {
        CapturedScreenPoint = PointToScreen(e.GetPosition(this));
        Close();
    }
}
