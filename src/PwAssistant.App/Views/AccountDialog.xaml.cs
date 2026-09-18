using System.Windows;
using PwAssistant.App.Resources;

namespace PwAssistant.App.Views;

public partial class AccountDialog : Window
{
    public string Login => LoginBox.Text.Trim();
    public string Password => PasswordBox.Password;
    public string Role => RoleBox.Text.Trim();

    public AccountDialog()
    {
        InitializeComponent();
        LoginLabel.Text = Strings.Login;
        PasswordLabel.Text = Strings.Password;
        RoleLabel.Text = Strings.Role;
        SaveButton.Content = Strings.Save;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Login) || string.IsNullOrEmpty(Password))
            return;
        DialogResult = true;
    }
}
