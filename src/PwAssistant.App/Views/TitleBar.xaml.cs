using System.Windows;
using System.Windows.Controls;

namespace PwAssistant.App.Views;

/// <summary>Themed caption bar for borderless windows: app icon, window
/// title and min/max/close buttons. Dragging and double-click maximize
/// come from WindowChrome's native caption — only the buttons are ours.
/// Buttons are focusable-free so the Mini remote never steals focus.</summary>
public partial class TitleBar : UserControl
{
    private const string MaximizeGlyph = "\uE922";
    private const string RestoreGlyph = "\uE923";

    public TitleBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private Window? Host => Window.GetWindow(this);

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (Host is not Window window) return;
        MaxButton.Visibility = window.ResizeMode is ResizeMode.CanResize or ResizeMode.CanResizeWithGrip
            ? Visibility.Visible
            : Visibility.Collapsed;
        window.StateChanged += (_, _) => UpdateMaxGlyph();
        UpdateMaxGlyph();
    }

    private void UpdateMaxGlyph() =>
        MaxButton.Content = Host?.WindowState == WindowState.Maximized ? RestoreGlyph : MaximizeGlyph;

    private void OnMinimize(object sender, RoutedEventArgs e)
    {
        if (Host is Window window)
            SystemCommands.MinimizeWindow(window);
    }

    private void OnMaximizeRestore(object sender, RoutedEventArgs e)
    {
        if (Host is not Window window) return;
        if (window.WindowState == WindowState.Maximized)
            SystemCommands.RestoreWindow(window);
        else
            SystemCommands.MaximizeWindow(window);
    }

    private void OnClose(object sender, RoutedEventArgs e)
    {
        if (Host is Window window)
            SystemCommands.CloseWindow(window);
    }
}
