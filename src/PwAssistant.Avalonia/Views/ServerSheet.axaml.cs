using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using PwAssistant.Avalonia.Resources;

namespace PwAssistant.Avalonia.Views;

/// <summary>In-window server editor sheet: blank stays open, result via
/// Task. File picker stays OS (Avalonia StorageProvider).</summary>
public partial class ServerSheet : UserControl
{
    private TaskCompletionSource<(bool Ok, string Name, string Path)>? _tcs;

    public ServerSheet()
    {
        InitializeComponent();
        NameLabel.Text = Strings.ServerName;
        SetClientPathLabel();
        SaveButton.Content = Strings.Save;
        CancelButton.Content = Strings.CancelDialog;
    }

    /// <summary>Single hint above the path input with the x64 folder in bold.
    /// ClientPath is a "{0}" format so word order stays translatable.</summary>
    private void SetClientPathLabel()
    {
        TextBlock? label = PathLabel;
        if (label is null) return;
        string[] parts = Strings.ClientPath.Split(["{0}"], StringSplitOptions.None);
        var inlines = new InlineCollection
        {
            new Run(parts[0]),
            new Run("x64") { FontWeight = FontWeight.Bold },
        };
        if (parts.Length > 1)
            inlines.Add(new Run(parts[1]));
        label.Inlines = inlines;
    }

    public Task<(bool Ok, string Name, string Path)> AskAsync(string? name, string? path)
    {
        if (_tcs is not null)
            _tcs.TrySetResult((false, string.Empty, string.Empty));
        NameBox.Text = name ?? string.Empty;
        PathBox.Text = path ?? string.Empty;
        IsVisible = true;
        _tcs = new TaskCompletionSource<(bool, string, string)>();
        Dispatcher.UIThread.Post(() => NameBox.Focus());
        return _tcs.Task;
    }

    private void Finish(bool ok, string name, string path)
    {
        IsVisible = false;
        TaskCompletionSource<(bool, string, string)>? tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult((ok, name, path));
    }

    private async void OnBrowse(object? sender, RoutedEventArgs e)
    {
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
            return;
        IReadOnlyList<IStorageFile> files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = string.Format(Strings.ClientPath, "x64"),
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("elementclient.exe")
                {
                    Patterns = ["elementclient*.exe"],
                },
                new FilePickerFileType("All executables")
                {
                    Patterns = ["*.exe"],
                },
            ],
        });
        if (files.Count > 0)
            PathBox.Text = files[0].TryGetLocalPath() ?? files[0].Name;
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        string name = NameBox.Text?.Trim() ?? string.Empty;
        string path = PathBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
            return;
        Finish(true, name, path);
    }

    private void OnCancel(object? sender, RoutedEventArgs e) =>
        Finish(false, string.Empty, string.Empty);

    private void OnDimmerDown(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, Dimmer))
            Finish(false, string.Empty, string.Empty);
    }

    private void OnSheetKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Finish(false, string.Empty, string.Empty);
        }
    }
}
