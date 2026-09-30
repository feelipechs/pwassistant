using System.Windows.Controls;

namespace PwAssistant.App.Views;

/// <summary>Overlay host for <see cref="Services.ToastService"/> items:
/// bottom-right, click-through, no code-behind state.</summary>
public partial class ToastHost : UserControl
{
    public ToastHost()
    {
        InitializeComponent();
    }
}
