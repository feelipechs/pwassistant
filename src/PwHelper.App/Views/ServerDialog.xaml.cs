using System.Windows;
using Microsoft.Win32;
using PwHelper.App.Resources;

namespace PwHelper.App.Views;

public partial class ServerDialog : Window
{
    public string ServerName => NameBox.Text.Trim();
    public string ClientPath => PathBox.Text.Trim();

    public ServerDialog()
    {
        InitializeComponent();
        NameLabel.Text = Strings.ServerName;
        PathLabel.Text = Strings.ClientPath;
        SaveButton.Content = Strings.Save;
    }

    private void OnBrowse(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "elementclient.exe|elementclient*.exe|All executables|*.exe" };
        if (dialog.ShowDialog() == true)
            PathBox.Text = dialog.FileName;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(ServerName) || string.IsNullOrWhiteSpace(ClientPath))
            return;
        DialogResult = true;
    }
}
