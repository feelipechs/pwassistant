using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using PwAssistant.Avalonia.Services;
using PwAssistant.Avalonia.ViewModels;
using PwAssistant.Avalonia.Views;
using PwAssistant.Core.Execution;
using PwAssistant.Core.Input;
using PwAssistant.Core.Launcher;
using PwAssistant.Core.Models;
using PwAssistant.Core.Storage;
using PwAssistant.Core.Sync;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.Avalonia;

public sealed class App : Application
{
    private ServiceProvider? _provider;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ThemeManager.Restore();
        ActualThemeVariantChanged += (_, _) => ThemeManager.RefreshAccent();

        // Avalonia handles nested scrolling natively: the WPF wheel-forwarding
        // class handlers (ComboBox/ScrollViewer) are intentionally not ported.

        var services = new ServiceCollection();
        var log = new FileLogger(FileLogger.DefaultDirectory());
        services.AddSingleton(log);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            log.Error("Unhandled: " + e.ExceptionObject);
        Dispatcher.UIThread.UnhandledException += (_, e) =>
            log.Error("UI thread: " + e.Exception);
        services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
        services.AddSingleton<IAccountStore, JsonFileAccountStore>();
        services.AddSingleton<IWindowResolver, WindowResolver>();
        services.AddSingleton<PostMessageBackgroundStrategy>();
        // Parked sender daemon (one process per session) + per-flush routing.
        // Disposed automatically with the provider at app exit.
        services.AddSingleton<SenderDaemon>();
        services.AddSingleton<SenderRunner>(sp =>
        {
            var daemon = sp.GetRequiredService<SenderDaemon>();
            var state = sp.GetRequiredService<AppState>();
            return (workload, cancellationToken) => daemon.RunAsync(
                workload, workload.Verbose || state.VerboseFireLog, cancellationToken);
        });
        services.AddSingleton<IInputStrategy>(sp => new FocusedInputStrategy(
            sp.GetRequiredService<PostMessageBackgroundStrategy>(),
            sp.GetRequiredService<AppState>(),
            sp.GetRequiredService<FileLogger>(),
            id => WorkerSenderRunner.ResolvePid(sp.GetRequiredService<AppState>(), id),
            sp.GetRequiredService<SenderRunner>()));
        services.AddSingleton<MouseHook>();
        services.AddSingleton<KeyboardHook>();
        services.AddSingleton<SyncService>();
        services.AddSingleton<FocusService>();
        services.AddSingleton<AppState>(sp => new AppState(
            sp.GetRequiredService<IAccountStore>(),
            sp.GetRequiredService<IWindowResolver>(),
            AppState.DefaultFilePath()));
        services.AddSingleton(sp => new GameLauncher(
            sp.GetRequiredService<IWindowResolver>(),
            def => ShortcutCreator.EnsureShortcut(
                def.ShortcutPath, def.TargetPath, def.Arguments,
                def.WorkingDirectory, def.IconLocation)));
        services.AddSingleton<MacroExecutor>(sp => new MacroExecutor(
            sp.GetRequiredService<IInputStrategy>(),
            id => sp.GetRequiredService<AppState>().ResolveTargetLive(id)));
        services.AddSingleton<MacroJobRunner>(sp => new MacroJobRunner(
            (preset, jobId, progress, ct) => sp.GetRequiredService<MacroExecutor>().ExecuteAsync(preset, jobId, progress, ct)));
        services.AddSingleton<PresetDispatcher>();
        services.AddSingleton<SyncController>();
        services.AddSingleton<FocusController>();
        services.AddSingleton<ClientWindowMarker>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<AppUpdater>();
        services.AddSingleton(sp => new LoopController(
            sp.GetRequiredService<PresetDispatcher>(),
            sp.GetRequiredService<FileLogger>(),
            sp.GetRequiredService<IWindowResolver>(),
            sp.GetRequiredService<AppState>()));
        services.AddTransient<MainViewModel>();
        services.AddTransient<GroupViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<GroupWindow>();
        services.AddTransient<MiniWindow>();
        services.AddSingleton<Func<MiniWindow>>(sp => () => sp.GetRequiredService<MiniWindow>());
        services.AddTransient<PresetEditorControl>();

        _provider = services.BuildServiceProvider();

        // Per-message fire trace (verbose flag in Settings, off by default).
        // Timestamp-correlates with the job lines from PresetFireLog.
        if (_provider.GetRequiredService<IInputStrategy>() is FocusedInputStrategy focused)
        {
            AppState state = _provider.GetRequiredService<AppState>();
            focused.Traced += trace =>
            {
                if (state.VerboseFireLog)
                    log.Info($"  [trace] hwnd=0x{trace.Hwnd:X} {trace.Tag} msg=0x{trace.Message:X} ok={(trace.Ok ? 1 : 0)} win32={trace.Win32Error}");
            };
        }

        var sync = _provider.GetRequiredService<SyncController>();
        sync.Start();
        var focus = _provider.GetRequiredService<FocusController>();
        focus.Start();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var main = _provider.GetRequiredService<MainWindow>();
            desktop.MainWindow = main;
            desktop.ShutdownRequested += (_, _) => AppShutdown.Request();
            desktop.Exit += (_, _) => DisposeServices();
            main.Show();
            _ = InitializeAndRegisterAsync(main);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Loads persisted data, then registers global preset hotkeys (which
    /// need the loaded preset list — registering earlier sees nothing).
    /// Observed fire-and-forget: startup failures are logged, never lost.
    /// </summary>
    private async Task InitializeAndRegisterAsync(MainWindow main)
    {
        try
        {
            await main.ViewModel.InitializeAsync().ConfigureAwait(false);
            await Dispatcher.UIThread.InvokeAsync(main.RegisterPresetHotkeys);
            // Observed fire-and-forget: the updater logs and never throws.
            _ = _provider?.GetService<AppUpdater>()?.CheckAndPromptAsync();
            // Sender pre-warm (delayed past startup): pays process + CLR +
            // JIT + disk/AV cache once per session with a pid-0 no-op
            // (resolves nothing, touches no window, exits 2). First real
            // fires then skip the cold-start stall. Never breaks startup.
            _ = WarmupSenderAsync();
        }
        catch (Exception ex)
        {
            _provider?.GetService<FileLogger>()?.Error($"Startup init failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Best-effort sender warmup ~10 s after startup (lets first paint and
    /// user settle first). A pid-0 workload is used on purpose: resolution
    /// fails before any window is touched (no focus call, no send), so the
    /// only effect is a warm process/JIT/disk cache. Always logs one line
    /// so a missing/slow warmup is visible instead of mysterious.
    /// </summary>
    private async Task WarmupSenderAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10)).ConfigureAwait(false);
            SenderRunner? runner = _provider?.GetService<SenderRunner>();
            FileLogger? log = _provider?.GetService<FileLogger>();
            if (runner is null || log is null)
                return;
            var workload = new SenderWorkload { ProcessId = 0, Verbose = false };
            _ = await runner(workload, CancellationToken.None).ConfigureAwait(false);
            // Any result (even pid-0 no-window) proves a sender process ran
            // end to end: process, CLR, JIT and disk/AV caches are now warm.
            log.Info("  [sender] warmup done");
        }
        catch (Exception ex)
        {
            _provider?.GetService<FileLogger>()?.Warn($"Sender warmup failed (first fire pays cold start): {ex.Message}");
        }
    }

    private void DisposeServices()
    {
        FileLogger? log = _provider?.GetService<FileLogger>();
        try
        {
            if (_provider is null)
                return;
            // Ordered shutdown (each stage logged + isolated: one stuck
            // dispose must be visible, never silent). Hooks first: a stuck
            // low-level hook stalls system-wide input, so it unhooks before
            // anything it could ever call back into is torn down.
            log?.Info("Shutdown: stopping input hooks.");
            TryStage(log, "SyncController", () => _provider.GetService<SyncController>()?.Dispose());
            TryStage(log, "FocusController", () => _provider.GetService<FocusController>()?.Dispose());
            log?.Info("Shutdown: stopping loops and jobs.");
            TryStage(log, "LoopController", () => _provider.GetService<LoopController>()?.Dispose());
            TryStage(log, "MacroJobRunner", () => _provider.GetService<MacroJobRunner>()?.Dispose());
            log?.Info("Shutdown: disposing provider.");
            TryStage(log, "ServiceProvider", () => _provider.Dispose());
            log?.Info("Shutdown: done.");
        }
        catch (Exception ex)
        {
            log?.Error($"Shutdown failed: {ex.Message}");
        }
        finally
        {
            _provider = null;
        }
    }

    private static void TryStage(FileLogger? log, string name, Action stage)
    {
        try
        {
            stage();
            log?.Info($"Shutdown: {name} ok.");
        }
        catch (Exception ex)
        {
            log?.Error($"Shutdown: {name} failed: {ex.Message}");
        }
    }
}
