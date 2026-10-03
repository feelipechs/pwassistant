using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Strings = PwAssistant.Avalonia.Resources.Strings;

namespace PwAssistant.Avalonia.Services;

/// <summary>Main-window background: a taskbar icon in the system tray.
/// Owned by MainWindow and disposed with it; never touches the game.</summary>
public sealed class TrayManager : IDisposable
{
    private TrayIcon? _icon;
    private bool _disposed;

    public event EventHandler? OpenRequested;
    public event EventHandler? ExitRequested;

    /// <summary>Shows the tray icon (idempotent). Caller hides its window.</summary>
    public void Show()
    {
        if (_disposed || Application.Current is null) return;
        if (_icon is not null)
        {
            _icon.IsVisible = true;
            return;
        }
        var menu = new NativeMenu();
        var open = new NativeMenuItem(Strings.TrayShow);
        open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        var exit = new NativeMenuItem(Strings.TrayExit);
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(open);
        menu.Items.Add(new NativeMenuItemSeparator());
        menu.Items.Add(exit);
        _icon = new TrayIcon
        {
            ToolTipText = Strings.TrayTip,
            Menu = menu,
        };
        WindowIcon? icon = TryLoadIcon();
        if (icon is not null)
            _icon.Icon = icon;
        // TODO(Avalonia): TrayIcon has no double-click event; Clicked restores
        // like the old WinForms double-click.
        _icon.Clicked += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        TrayIcons icons = TrayIcon.GetIcons(Application.Current) ?? new TrayIcons();
        if (TrayIcon.GetIcons(Application.Current) is null)
            TrayIcon.SetIcons(Application.Current, icons);
        icons.Add(_icon);
    }

    public void Hide()
    {
        if (_icon is not null)
            _icon.IsVisible = false;
    }

    /// <summary>Fallback for toasts when no app window is visible.</summary>
    public void ShowBalloon(string title, string message)
    {
        // TODO(Avalonia): TrayIcon has no balloon-tip API; overlay toasts
        // cover this path, so this stays a no-op by design.
    }

    private static WindowIcon? TryLoadIcon()
    {
        try
        {
            using Stream stream = AssetLoader.Open(new Uri("avares://PwAssistant.Avalonia/Resources/App.ico"));
            return new WindowIcon(stream);
        }
        catch
        {
            return null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_icon is not null)
        {
            try
            {
                if (Application.Current is not null)
                    TrayIcon.GetIcons(Application.Current)?.Remove(_icon);
                _icon.Dispose();
            }
            catch
            {
                // Best effort by contract.
            }
            _icon = null;
        }
    }
}
