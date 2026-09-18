using System.Windows;
using PwAssistant.App.Resources;
using PwAssistant.Core.Models;

namespace PwAssistant.App.Views;

public partial class AccountDialog : Window
{
    public string Login => LoginBox.Text.Trim();
    public string Password => PasswordBox.Password;
    public string Role => RoleBox.Text.Trim();
    public string? Nickname => SameAsRoleCheck.IsChecked == true
        ? null
        : (string.IsNullOrWhiteSpace(NicknameBox.Text) ? null : NicknameBox.Text.Trim());
    public string? Class => (ClassBox.SelectedItem as ClassInfo)?.Key;

    /// <summary>When false (edit mode), an empty password keeps the stored one.</summary>
    public bool RequirePassword { get; set; } = true;

    public AccountDialog()
    {
        InitializeComponent();
        LoginLabel.Text = Strings.Login;
        PasswordLabel.Text = Strings.Password;
        RoleLabel.Text = Strings.Role;
        NicknameLabel.Text = Strings.Nickname;
        SameAsRoleCheck.Content = Strings.SameAsRole;
        SameAsRoleCheck.IsChecked = true;
        ClassLabel.Text = Strings.Class;
        ClassBox.ItemsSource = ClassCatalog.All;
        ClassBox.SelectedIndex = -1;
        SaveButton.Content = Strings.Save;
        SameAsRoleCheck.Checked += (_, _) => MirrorRole();
        SameAsRoleCheck.Unchecked += (_, _) => NicknameBox.IsEnabled = true;
        RoleBox.TextChanged += (_, _) => MirrorRole();
    }

    /// <summary>While same-as-role holds, the nickname mirrors the role live.</summary>
    private void MirrorRole()
    {
        if (SameAsRoleCheck.IsChecked == true)
        {
            NicknameBox.Text = RoleBox.Text;
            NicknameBox.IsEnabled = false;
        }
        else
        {
            NicknameBox.IsEnabled = true;
        }
    }

    public void Prefill(Account account)
    {
        LoginBox.Text = account.Login;
        RoleBox.Text = account.Role;
        NicknameBox.Text = account.Nickname ?? string.Empty;
        SameAsRoleCheck.IsChecked = string.IsNullOrWhiteSpace(account.Nickname);
        ClassBox.SelectedItem = ClassCatalog.TryGet(account.Class, out ClassInfo info) ? info : null;
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
