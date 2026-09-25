using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PwAssistant.App.Resources;

namespace PwAssistant.App.Views;

/// <summary>In-window text prompt sheet (replaces the TextPromptDialog
/// window): label + input, blank stays open, result via Task.</summary>
public partial class PromptSheet : UserControl
{
    private TaskCompletionSource<(bool Ok, string Value)>? _tcs;

    public PromptSheet()
    {
        InitializeComponent();
        SaveButton.Content = Strings.Save;
        CancelButton.Content = Strings.CancelDialog;
    }

    /// <summary>Shows the sheet; blank input keeps it open (dialog parity).</summary>
    public Task<(bool Ok, string Value)> AskAsync(string labelKey, string initial)
    {
        if (_tcs is not null)
            _tcs.TrySetResult((false, string.Empty));
        PromptLabel.Text = Strings.Get(labelKey);
        ValueBox.Text = initial;
        Visibility = Visibility.Visible;
        _tcs = new TaskCompletionSource<(bool, string)>();
        Dispatcher.InvokeAsync(() =>
        {
            ValueBox.Focus();
            ValueBox.SelectAll();
        });
        return _tcs.Task;
    }

    private void Finish(bool ok, string value)
    {
        Visibility = Visibility.Collapsed;
        TaskCompletionSource<(bool, string)>? tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult((ok, value));
    }

    private static string CurrentText(TextBox box) => box.Text.Trim();

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CurrentText(ValueBox)))
            return;
        Finish(true, CurrentText(ValueBox));
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Finish(false, string.Empty);

    private void OnValueKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OnSave(sender, e);
        }
    }

    private void OnDimmerDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Dimmer))
            Finish(false, string.Empty);
    }

    private void OnSheetKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Finish(false, string.Empty);
        }
    }
}
