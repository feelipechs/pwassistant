using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using PwAssistant.App.Services;
using PwAssistant.App.ViewModels;
using PwAssistant.App.Views;
using PwAssistant.Core.Execution;
using PwAssistant.Core.Input;
using PwAssistant.Core.Launcher;
using PwAssistant.Core.Models;
using PwAssistant.Core.Storage;
using PwAssistant.Core.Sync;
using PwAssistant.WinApi;

namespace PwAssistant.App;

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
        services.AddSingleton<KeyboardHook>();
        services.AddSingleton<SyncService>();
        services.AddSingleton<FocusService>();
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
        services.AddSingleton<FocusController>();
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
        var focus = _provider.GetRequiredService<FocusController>();
        focus.Start();

        var main = _provider.GetRequiredService<MainWindow>();
        MainWindow = main;
        main.Show();
        _ = InitializeAndRegisterAsync(main);
    }

    /// <summary>
    /// Loads persisted data, then registers global preset hotkeys (which
    /// need the loaded preset list — registering earlier sees nothing).
    /// </summary>
    private static async Task InitializeAndRegisterAsync(MainWindow main)
    {
        await main.ViewModel.InitializeAsync().ConfigureAwait(false);
        await main.Dispatcher.InvokeAsync(main.RegisterPresetHotkeys).Task.ConfigureAwait(false);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_provider is not null)
        {
            _provider.GetService<SyncController>()?.Dispose();
            _provider.GetService<FocusController>()?.Dispose();
            _provider.GetService<MacroJobRunner>()?.Dispose();
            _provider.Dispose();
        }
        base.OnExit(e);
    }
}
