using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PwHelper.App.Services;
using PwHelper.App.Views;
using PwHelper.Core.Launcher;
using PwHelper.Core.Models;
using AppStrings = PwHelper.App.Resources.Strings;

namespace PwHelper.App.ViewModels;

public sealed partial class AccountCard : ObservableObject
{
    public Account Model { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private AccountStatus status;

    public AccountCard(Account model)
    {
        Model = model;
        status = model.Status;
    }

    public string DisplayName => string.IsNullOrWhiteSpace(Model.Role) ? Model.Login : Model.Role;
    public string StatusText => Status == AccountStatus.Online ? "Online" : "Offline";

    public void Refresh() => Status = Model.Status;
}

public sealed partial class MainViewModel : ObservableObject
{
    private readonly AppState _state;
    private readonly GameLauncher _launcher;
    private readonly PresetDispatcher _dispatcher;
    private readonly IServiceProvider _services;

    public ObservableCollection<Server> Servers { get; } = new();
    public ObservableCollection<AccountCard> Accounts { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AccountsHeader))]
    private Server? selectedServer;

    public string AccountsHeader => SelectedServer is null
        ? AppStrings.NoServerSelected
        : string.Format(AppStrings.AccountsOfServer, SelectedServer.Name);

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public MainViewModel(AppState state, GameLauncher launcher, PresetDispatcher dispatcher, IServiceProvider services)
    {
        _state = state;
        _launcher = launcher;
        _dispatcher = dispatcher;
        _services = services;
    }

    public async Task InitializeAsync()
    {
        await _state.LoadAsync().ConfigureAwait(false);
        App.Current.Dispatcher.Invoke(() =>
        {
            Servers.Clear();
            foreach (Server server in _state.Data.Servers)
                Servers.Add(server);
            SelectedServer = _state.SelectedServer ?? Servers.FirstOrDefault();
        });
    }

    partial void OnSelectedServerChanged(Server? value)
    {
        _state.SelectedServer = value;
        Accounts.Clear();
        if (value is null) return;
        foreach (Account account in value.Accounts)
            Accounts.Add(new AccountCard(account));
    }

    [RelayCommand]
    private async Task AddServerAsync()
    {
        var dialog = new ServerDialog();
        if (dialog.ShowDialog() == true)
        {
            var server = new Server { Name = dialog.ServerName, ElementClientPath = dialog.ClientPath };
            _state.Data.Servers.Add(server);
            Servers.Add(server);
            SelectedServer = server;
            await _state.SaveAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task AddAccountAsync()
    {
        if (SelectedServer is null) return;
        var dialog = new AccountDialog();
        if (dialog.ShowDialog() == true)
        {
            var account = new Account
            {
                ServerId = SelectedServer.Id,
                Login = dialog.Login,
                Role = dialog.Role
            };
            _state.SetPassword(account, dialog.Password);
            SelectedServer.Accounts.Add(account);
            Accounts.Add(new AccountCard(account));
            await _state.SaveAsync().ConfigureAwait(false);
        }
    }

    [RelayCommand]
    private async Task PlayAsync(AccountCard? card)
    {
        if (card is null || SelectedServer is null) return;
        try
        {
            StatusMessage = "...";
            string password = _state.RevealPassword(card.Model);
            GameSession session = await _launcher.LaunchAsync(
                SelectedServer, card.Model, password, TimeSpan.FromSeconds(60)).ConfigureAwait(false);
            WatchSession(session, card);
            await _state.SaveAsync().ConfigureAwait(false);
            App.Current.Dispatcher.Invoke(card.Refresh);
            StatusMessage = string.Empty;
        }
        catch (Exception ex)
        {
            StatusMessage = ex.Message;
        }
    }

    /// <summary>
    /// M6 crash handling: when the client dies, cancel running jobs and
    /// mark the account offline.
    /// </summary>
    private void WatchSession(GameSession session, AccountCard card)
    {
        try
        {
            Process process = Process.GetProcessById(session.ProcessId);
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) =>
            {
                _dispatcher.CancelAll();
                card.Model.ProcessId = null;
                card.Model.WindowHandle = IntPtr.Zero;
                App.Current.Dispatcher.Invoke(card.Refresh);
            };
        }
        catch (ArgumentException)
        {
            // Process already gone; status refresh will show it offline.
        }
    }

    [RelayCommand]
    private void OpenGroupMode()
    {
        var window = (GroupWindow)_services.GetService(typeof(GroupWindow))!;
        window.Show();
    }

    [RelayCommand]
    private void RefreshStatus()
    {
        _state.RefreshOnlineStatus();
        foreach (AccountCard card in Accounts)
            card.Refresh();
    }
}
