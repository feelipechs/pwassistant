using WinForms = System.Windows.Forms;
using Drawing = System.Drawing;
using Strings = PwAssistant.App.Resources.Strings;

namespace PwAssistant.App.Services;

/// <summary>Main-window background: a taskbar icon in the system tray.
/// Owned by MainWindow and disposed with it; never touches the game.
/// WinForms/System.Drawing are aliased here because their implicit
/// usings are removed project-wide (they clash with WPF types).</summary>
public sealed class TrayManager : IDisposable
{
    private WinForms.NotifyIcon? _icon;
    private bool _disposed;

    public event EventHandler? OpenRequested;
    public event EventHandler? ExitRequested;

    /// <summary>Shows the tray icon (idempotent). Caller hides its window.</summary>
    public void Show()
    {
        if (_disposed) return;
        if (_icon is not null)
        {
            _icon.Visible = true;
            return;
        }
        var menu = new WinForms.ContextMenuStrip();
        var open = new WinForms.ToolStripMenuItem(Strings.TrayShow);
        open.Click += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
        var exit = new WinForms.ToolStripMenuItem(Strings.TrayExit);
        exit.Click += (_, _) => ExitRequested?.Invoke(this, EventArgs.Empty);
        menu.Items.Add(open);
        menu.Items.Add(new WinForms.ToolStripSeparator());
        menu.Items.Add(exit);
        Drawing.Icon? icon = null;
        try
        {
            icon = Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? string.Empty);
        }
        catch (Exception)
        {
            // Fall through to SystemIcons.Application below.
        }
        _icon = new WinForms.NotifyIcon
        {
            Icon = icon ?? Drawing.SystemIcons.Application,
            Text = Strings.TrayTip,
            ContextMenuStrip = menu,
            Visible = true,
        };
        _icon.DoubleClick += (_, _) => OpenRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Hide()
    {
        if (_icon is not null)
            _icon.Visible = false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
