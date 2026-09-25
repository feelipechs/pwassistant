using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PwAssistant.App.Resources;
using PwAssistant.Core.Models;

namespace PwAssistant.App.Views;

/// <summary>Draft exchanged with the account sheet (never logged/stored).</summary>
public sealed record AccountDraft(
    string Login,
    string Password,
    string Role,
    string? Nickname,
    string? ClassKey,
    string? Tag);

/// <summary>In-window account editor sheet (replaces the AccountDialog
/// window): same fields, validation and tag rules, result via Task.</summary>
public partial class AccountSheet : UserControl
{
    private TaskCompletionSource<AccountDraft?>? _tcs;
    private bool _requirePassword = true;

    public AccountSheet()
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
        TagLabel.Text = Strings.Tag;
        SaveButton.Content = Strings.Save;
        CancelButton.Content = Strings.CancelDialog;
        SameAsRoleCheck.Checked += (_, _) => MirrorRole();
        SameAsRoleCheck.Unchecked += (_, _) => NicknameBox.IsEnabled = true;
        RoleBox.TextChanged += (_, _) => MirrorRole();
    }

    public Task<AccountDraft?> EditAsync(
        AccountDraft initial, IEnumerable<string> knownTags, string? lockTag, bool requirePassword)
    {
        if (_tcs is not null)
            _tcs.TrySetResult(null);
        _requirePassword = requirePassword;
        LoginBox.Text = initial.Login;
        PasswordBox.Clear();
        RoleBox.Text = initial.Role;
        NicknameBox.Text = initial.Nickname ?? string.Empty;
        SameAsRoleCheck.IsChecked = string.IsNullOrWhiteSpace(initial.Nickname);
        ClassBox.SelectedItem = initial.ClassKey is not null
            && ClassCatalog.TryGet(initial.ClassKey, out ClassInfo info)
            ? info
            : null;
        TagBox.ItemsSource = knownTags.ToList();
        if (lockTag is null)
        {
            TagBox.IsEnabled = true;
            SelectTag(initial.Tag);
        }
        else
        {
            SelectTag(lockTag);
            TagBox.IsEnabled = false;
        }
        MirrorRole();
        Visibility = Visibility.Visible;
        _tcs = new TaskCompletionSource<AccountDraft?>();
        Dispatcher.InvokeAsync(() => LoginBox.Focus());
        return _tcs.Task;
    }

    private void Finish(AccountDraft? draft)
    {
        Visibility = Visibility.Collapsed;
        TaskCompletionSource<AccountDraft?>? tcs = _tcs;
        _tcs = null;
        tcs?.TrySetResult(draft);
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

    private void SelectTag(string? tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            TagBox.SelectedItem = null;
            return;
        }
        foreach (string known in TagBox.Items)
        {
            if (string.Equals(known, tag, StringComparison.OrdinalIgnoreCase))
            {
                TagBox.SelectedItem = known;
                return;
            }
        }
        // Legacy tag without a tab: keep it selectable instead of losing data.
        var items = TagBox.Items.Cast<string>().ToList();
        items.Add(tag);
        TagBox.ItemsSource = items;
        TagBox.SelectedItem = tag;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        string login = LoginBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(login))
            return;
        string password = PasswordBox.Password;
        if (_requirePassword && string.IsNullOrEmpty(password))
            return;
        Finish(new AccountDraft(
            login,
            password,
            RoleBox.Text.Trim(),
            SameAsRoleCheck.IsChecked == true
                ? null
                : (string.IsNullOrWhiteSpace(NicknameBox.Text) ? null : NicknameBox.Text.Trim()),
            (ClassBox.SelectedItem as ClassInfo)?.Key,
            TagBox.SelectedItem as string));
    }

    private void OnCancel(object sender, RoutedEventArgs e) => Finish(null);

    private void OnDimmerDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, Dimmer))
            Finish(null);
    }

    private void OnSheetKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Finish(null);
        }
    }
}
