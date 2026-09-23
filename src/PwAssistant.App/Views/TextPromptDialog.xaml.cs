using System.Windows;
using PwAssistant.App.Resources;

using PwAssistant.App.Services;

namespace PwAssistant.App.Views;

public partial class TextPromptDialog : Window
{
    public string Value => ValueBox.Text.Trim();

    public TextPromptDialog(string labelKey, string initial)
    {
        InitializeComponent();
        DialogOwner.Own(this);
        PromptLabel.Text = Strings.Get(labelKey);
        ValueBox.Text = initial;
        SaveButton.Content = Strings.Save;
        CancelButton.Content = Strings.CancelDialog;
        Loaded += (_, _) => ValueBox.Focus();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Value))
            return;
        DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
