using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PwAssistant.App.Resources;

namespace PwAssistant.App.Views;

/// <summary>In-window confirm sheet (replaces the ConfirmDialog window):
/// dimmer + card, click-outside/ESC cancels, result via Task.</summary>
public partial class ConfirmSheet : UserControl
{
    private TaskCompletionSource<bool>? _tcs;

    public ConfirmSheet()
    {
        InitializeComponent();
        CancelButton.Content = Strings.CancelDialog;
    }

    /// <summary>Shows the sheet; completes true on confirm, false otherwise.</summary>
    public Task<bool> AskAsync(string title, string message, string confirmLabel)
    {
        if (_tcs is not null)
            _tcs.TrySetResult(false);
        TitleLabel.Text = title;
        MessageLabel.Text = message;
        ConfirmButton.Content = confirmLabel;
        Visibility = Visibility.Visible;
        _tcs = new TaskCompletionSource<bool>();
        Dispatcher.InvokeAsync(() => ConfirmButton.Focus());
        return _tcs.Task;
    }

    private void Finish(bool ok)
    {
        Visibility = Visibility.Collapsed;
        TaskCompletionSource<bool>? tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult(ok);
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => Finish(true);

    private void OnCancel(object sender, RoutedEventArgs e) => Finish(false);

    private void OnDimmerDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Dimmer))
            Finish(false);
    }

    private void OnSheetKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Finish(false);
        }
    }
}
