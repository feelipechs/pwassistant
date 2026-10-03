using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using PwAssistant.Avalonia.Resources;

namespace PwAssistant.Avalonia.Views;

/// <summary>In-window confirm sheet: dimmer + card, click-outside/ESC
/// cancels, result via Task.</summary>
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
        IsVisible = true;
        _tcs = new TaskCompletionSource<bool>();
        Dispatcher.UIThread.Post(() => ConfirmButton.Focus());
        return _tcs.Task;
    }

    private void Finish(bool ok)
    {
        IsVisible = false;
        TaskCompletionSource<bool>? tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult(ok);
    }

    private void OnConfirm(object? sender, RoutedEventArgs e) => Finish(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Finish(false);

    private void OnDimmerDown(object? sender, PointerPressedEventArgs e)
    {
        if (ReferenceEquals(e.Source, Dimmer))
            Finish(false);
    }

    private void OnSheetKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Finish(false);
        }
    }
}
