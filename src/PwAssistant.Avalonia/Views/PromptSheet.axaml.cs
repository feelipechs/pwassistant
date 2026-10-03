using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PwAssistant.Avalonia.Resources;

namespace PwAssistant.Avalonia.Views;

/// <summary>In-window text prompt sheet: label + input, blank stays open,
/// result via Task.</summary>
public partial class PromptSheet : UserControl
{
    private TaskCompletionSource<(bool Ok, string Value)>? _tcs;

    public PromptSheet()
    {
        InitializeComponent();
        SaveButton.Content = Strings.Save;
        CancelButton.Content = Strings.CancelDialog;
    }

    /// <summary>Shows the sheet; blank input keeps it open.</summary>
    public Task<(bool Ok, string Value)> AskAsync(string labelKey, string initial)
    {
        if (_tcs is not null)
            _tcs.TrySetResult((false, string.Empty));
        PromptLabel.Text = Strings.Get(labelKey);
        ValueBox.Text = initial;
        IsVisible = true;
        _tcs = new TaskCompletionSource<(bool, string)>();
        Dispatcher.UIThread.Post(() =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        });
        return _tcs.Task;
    }

    private void Finish(bool ok, string value)
    {
        IsVisible = false;
        TaskCompletionSource<(bool, string)>? tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult((ok, value));
    }

    private static string CurrentText(TextBox box) => box.Text?.Trim() ?? string.Empty;

    private void TrySave()
    {
        if (string.IsNullOrWhiteSpace(CurrentText(ValueBox)))
            return;
        Finish(true, CurrentText(ValueBox));
    }

    private void OnSave(object? sender, RoutedEventArgs e) => TrySave();

    private void OnCancel(object? sender, RoutedEventArgs e) => Finish(false, string.Empty);

    private void OnValueKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            TrySave();
        }
    }

    private void OnDimmerDown(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, Dimmer))
            Finish(false, string.Empty);
    }

    private void OnSheetKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Finish(false, string.Empty);
        }
    }
}
