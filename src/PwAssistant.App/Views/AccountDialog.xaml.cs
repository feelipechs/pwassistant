using System.Windows;
using PwAssistant.App.Resources;

namespace PwAssistant.App.Views;

public partial class AccountDialog : Window
{
    public string Login => LoginBox.Text.Trim();
    public string Password => PasswordBox.Password;
    public string Role => RoleBox.Text.Trim();

    /// <summary>When false (edit mode), an empty password keeps the stored one.</summary>
    public bool RequirePassword { get; set; } = true;

    public AccountDialog()
    {
        InitializeComponent();
        LoginLabel.Text = Strings.Login;
        PasswordLabel.Text = Strings.Password;
        RoleLabel.Text = Strings.Role;
        SaveButton.Content = Strings.Save;
    }

    public void Prefill(string login, string role)
    {
        LoginBox.Text = login;
        RoleBox.Text = role;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Login))
            return;
        if (RequirePassword && string.IsNullOrEmpty(Password))
            return;
        DialogResult = true;
    }
}
