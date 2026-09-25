using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using PwAssistant.App.Resources;

namespace PwAssistant.App.Views;

/// <summary>In-window server editor sheet (replaces the ServerDialog
/// window): blank stays open, result via Task. File picker stays OS.</summary>
public partial class ServerSheet : UserControl
{
    private TaskCompletionSource<(bool Ok, string Name, string Path)>? _tcs;

    public ServerSheet()
    {
        InitializeComponent();
        NameLabel.Text = Strings.ServerName;
        PathLabel.Text = Strings.ClientPath;
        SaveButton.Content = Strings.Save;
        CancelButton.Content = Strings.CancelDialog;
    }

    public Task<(bool Ok, string Name, string Path)> AskAsync(string? name, string? path)
    {
        if (_tcs is not null)
            _tcs.TrySetResult((false, string.Empty, string.Empty));
        NameBox.Text = name ?? string.Empty;
        PathBox.Text = path ?? string.Empty;
        Visibility = Visibility.Visible;
        _tcs = new TaskCompletionSource<(bool, string, string)>();
        Dispatcher.InvokeAsync(() => NameBox.Focus());
        return _tcs.Task;
    }

    private void Finish(bool ok, string name, string path)
    {
        Visibility = Visibility.Collapsed;
        TaskCompletionSource<(bool, string, string)>? tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult((ok, name, path));
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "elementclient.exe|elementclient*.exe|All executables|*.exe" };
        if (dialog.ShowDialog() == true)
            PathBox.Text = dialog.FileName;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        string name = NameBox.Text.Trim();
        string path = PathBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
            return;
        Finish(true, name, path);
    }

    private void OnCancel(object sender, RoutedEventArgs e) =>
        Finish(false, string.Empty, string.Empty);

    private void OnDimmerDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Dimmer))
            Finish(false, string.Empty, string.Empty);
    }

    private void OnSheetKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Finish(false, string.Empty, string.Empty);
        }
    }
}
