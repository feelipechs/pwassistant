using System.Windows;
using PwHelper.App.Resources;

namespace PwHelper.App.Views;

public partial class TextPromptDialog : Window
{
    public string Value => ValueBox.Text.Trim();

    public TextPromptDialog(string labelKey, string initial)
    {
        InitializeComponent();
        PromptLabel.Text = Strings.Get(labelKey);
        ValueBox.Text = initial;
        SaveButton.Content = Strings.Save;
        Loaded += (_, _) => ValueBox.Focus();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Value))
            return;
        DialogResult = true;
    }
}
