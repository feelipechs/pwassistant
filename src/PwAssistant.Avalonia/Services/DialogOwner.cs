using Avalonia.Controls;

namespace PwAssistant.Avalonia.Services;

/// <summary>
/// Centers VM-opened dialogs on the app. Avalonia windows are independent
/// by design (owned windows trap siblings above the owner), and sheets are
/// in-window overlays — so there is nothing to own: kept as the seam for
/// future true dialogs.
/// </summary>
public static class DialogOwner
{
    public static void Own(Window window)
    {
    }
}
