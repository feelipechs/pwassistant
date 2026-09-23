using System.Windows;
using Microsoft.Win32;
using PwAssistant.App.Resources;
using PwAssistant.Core.Models;

using PwAssistant.App.Services;

namespace PwAssistant.App.Views;

public partial class ServerDialog : Window
{
    public string ServerName => NameBox.Text.Trim();
    public string ClientPath => PathBox.Text.Trim();

    public ServerDialog()
    {
        InitializeComponent();
        DialogOwner.Own(this);
        NameLabel.Text = Strings.ServerName;
        PathLabel.Text = Strings.ClientPath;
        SaveButton.Content = Strings.Save;
        CancelButton.Content = Strings.CancelDialog;
    }

    public void Prefill(Server server)
    {
        NameBox.Text = server.Name;
        PathBox.Text = server.ElementClientPath;
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

    private void OnCancel(object sender, RoutedEventArgs e) => Close();
}
