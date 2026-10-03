using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using PwAssistant.WinApi;

namespace PwAssistant.Avalonia.Views;

/// <summary>
/// Native-frame wiring: app icon + caption following the app theme.
/// Called from window constructors; the HWND only exists once shown, so
/// everything runs on Opened. No custom chrome anymore (TitleBar retired):
/// resize, snap and system menu are the OS ones, like the WPF era.
/// </summary>
internal static class WindowChrome
{
    private static global::Avalonia.Controls.WindowIcon? _appIcon;

    /// <param name="isDark">Resolves the current theme (Dark = dark caption).</param>
    public static void ApplyNative(Window window, Func<bool> isDark)
    {
        window.Opened += (_, _) =>
        {
            ApplyIcon(window);
            IntPtr handle = (window as TopLevel)?.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            CaptionTheme.Apply(handle, isDark());
        };
    }

    private static void ApplyIcon(Window window)
    {
        try
        {
            _appIcon ??= LoadIcon();
            if (_appIcon is not null && window.Icon is null)
                window.Icon = _appIcon;
        }
        catch
        {
            // A missing icon never breaks a window.
        }
    }

    private static global::Avalonia.Controls.WindowIcon? LoadIcon()
    {
        try
        {
            using Stream stream = AssetLoader.Open(
                new Uri("avares://PwAssistant.Avalonia/Resources/App.ico"));
            return new global::Avalonia.Controls.WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }
}
