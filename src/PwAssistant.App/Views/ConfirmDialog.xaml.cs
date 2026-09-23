using System.Windows;
using PwAssistant.App.Resources;
using PwAssistant.App.Services;

namespace PwAssistant.App.Views;

/// <summary>Themed delete confirmation (replaces the OS MessageBox).</summary>
public partial class ConfirmDialog : Window
{
    public ConfirmDialog()
    {
        InitializeComponent();
        DialogOwner.Own(this);
        CancelButton.Content = Strings.CancelDialog;
    }

    private void OnConfirm(object sender, RoutedEventArgs e) => DialogResult = true;

    private void OnCancel(object sender, RoutedEventArgs e) => Close();

    /// <summary>Shows the dialog; returns true when the user confirms.</summary>
    public static bool Ask(string title, string message, string confirmLabel)
    {
        var dialog = new ConfirmDialog
        {
            Title = title,
        };
        dialog.MessageLabel.Text = message;
        dialog.ConfirmButton.Content = confirmLabel;
        return dialog.ShowDialog() == true;
    }
}
