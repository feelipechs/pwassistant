using System.Windows;

namespace PwAssistant.App.Services;

/// <summary>
/// Centers VM-opened dialogs on the app: assigns the focused window
/// (falling back to MainWindow) as Owner. Called from window constructors
/// so no ViewModel needs a view reference.
/// </summary>
public static class DialogOwner
{
    public static void Own(Window window)
    {
        Window? owner = Application.Current?.Windows.OfType<Window>()
            .FirstOrDefault(w => w.IsActive && w != window)
            ?? Application.Current?.MainWindow;
        if (owner is not null && owner != window)
            window.Owner = owner;
    }
}
