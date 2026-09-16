using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PwHelper.App.Services;
using PwHelper.App.ViewModels;
using PwHelper.App.Views;
using PwHelper.Core.Execution;
using PwHelper.Core.Input;
using PwHelper.Core.Launcher;
using PwHelper.Core.Models;
using PwHelper.Core.Storage;
using PwHelper.Core.Sync;
using PwHelper.WinApi;

namespace PwHelper.App;

public partial class App : Application
{
    private ServiceProvider? _provider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<IAccountStore, JsonFileAccountStore>();
        services.AddSingleton<IWindowResolver, WindowResolver>();
        services.AddSingleton<IInputStrategy, PostMessageBackgroundStrategy>();
        services.AddSingleton<MouseHook>();
        services.AddSingleton<SyncService>();
        services.AddSingleton<AppState>(sp => new AppState(
            sp.GetRequiredService<IAccountStore>(),
            sp.GetRequiredService<IWindowResolver>(),
            AppState.DefaultFilePath()));
        services.AddSingleton<GameLauncher>();
        services.AddSingleton<MacroExecutor>(sp => new MacroExecutor(
            sp.GetRequiredService<IInputStrategy>(),
            id => sp.GetRequiredService<AppState>().ResolveTarget(id)));
        services.AddSingleton<MacroJobRunner>(sp => new MacroJobRunner(
            (preset, jobId, ct) => sp.GetRequiredService<MacroExecutor>().ExecuteAsync(preset, jobId, ct)));
        services.AddSingleton<PresetDispatcher>();
        services.AddSingleton<SyncController>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<GroupViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<GroupWindow>();
        services.AddTransient<Func<Preset, PresetEditor>>(sp => preset => new PresetEditor(
            sp.GetRequiredService<AppState>(),
            sp.GetRequiredService<IWindowResolver>(),
            preset));

        _provider = services.BuildServiceProvider();

        var sync = _provider.GetRequiredService<SyncController>();
        sync.Start();

        var main = _provider.GetRequiredService<MainWindow>();
        MainWindow = main;
        main.Show();
        _ = main.ViewModel.InitializeAsync();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_provider is not null)
        {
            _provider.GetService<SyncController>()?.Dispose();
            _provider.GetService<MacroJobRunner>()?.Dispose();
            _provider.Dispose();
        }
        base.OnExit(e);
    }
}
