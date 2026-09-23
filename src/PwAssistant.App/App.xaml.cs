using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
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
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.App;

public partial class App : Application
{
    private ServiceProvider? _provider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Closed ComboBoxes must not steal the wheel (it would flip the
        // selection under the cursor); forward it to the parent scroller.
        EventManager.RegisterClassHandler(
            typeof(ComboBox),
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(ForwardWheelToScroller));
        // Nested scrollers (card lists, mini lists) keep the wheel only
        // while they can move; at either extent it bubbles to the parent.
        EventManager.RegisterClassHandler(
            typeof(ScrollViewer),
            UIElement.PreviewMouseWheelEvent,
            new MouseWheelEventHandler(ForwardWheelAtExtent));

        var services = new ServiceCollection();
        var log = new FileLogger(FileLogger.DefaultDirectory());
        services.AddSingleton(log);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            log.Error("Unhandled: " + e.ExceptionObject);
        DispatcherUnhandledException += (_, e) =>
            log.Error("UI thread: " + e.Exception);
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
        services.AddSingleton(sp => new GameLauncher(
            sp.GetRequiredService<IWindowResolver>(),
            def => ShortcutCreator.EnsureShortcut(
                def.ShortcutPath, def.TargetPath, def.Arguments,
                def.WorkingDirectory, def.IconLocation)));
        services.AddSingleton<MacroExecutor>(sp => new MacroExecutor(
            sp.GetRequiredService<IInputStrategy>(),
            id => sp.GetRequiredService<AppState>().ResolveTarget(id)));
        services.AddSingleton<MacroJobRunner>(sp => new MacroJobRunner(
            (preset, jobId, progress, ct) => sp.GetRequiredService<MacroExecutor>().ExecuteAsync(preset, jobId, progress, ct)));
        services.AddSingleton<PresetDispatcher>();
        services.AddSingleton<SyncController>();
        services.AddSingleton<FocusController>();
        services.AddSingleton(sp => new LoopController(
            sp.GetRequiredService<PresetDispatcher>(),
            sp.GetRequiredService<FileLogger>()));
        services.AddTransient<MainViewModel>();
        services.AddTransient<GroupViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<GroupWindow>();
        services.AddTransient<MiniWindow>();
        services.AddSingleton<Func<MiniWindow>>(sp => () => sp.GetRequiredService<MiniWindow>());
        services.AddTransient<Func<Preset, PresetEditor>>(sp => preset => new PresetEditor(
            sp.GetRequiredService<AppState>(),
            sp.GetRequiredService<IWindowResolver>(),
            preset,
            sp.GetRequiredService<KeyboardHook>(),
            sp.GetRequiredService<SyncController>()));

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
    /// Wheel over a closed ComboBox scrolls the page instead of changing
    /// the selection. An open dropdown keeps the native item navigation.
    /// </summary>
    private static void ForwardWheelToScroller(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ComboBox combo || combo.IsDropDownOpen) return;
        e.Handled = true;
        ForwardToParentScroller(combo, e);
    }

    /// <summary>
    /// A scroller that cannot move further in the wheel direction yields to
    /// its parent instead of swallowing the gesture (nested card/mini lists).
    /// </summary>
    private static void ForwardWheelAtExtent(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scroller) return;
        // A scroller with no extent must never swallow the gesture (e.g. a
        // SizeToContent dialog behind an open ComboBox dropdown in a Popup).
        if (scroller.ScrollableHeight <= 0) return;
        // Tunnel order reaches the outer scroller first: yield when a nested
        // scroller under the cursor will decide for itself.
        if (HasNestedScroller(scroller, e)) return;
        bool canUp = scroller.VerticalOffset > 0;
        bool canDown = scroller.VerticalOffset < scroller.ScrollableHeight;
        if ((e.Delta > 0 && canUp) || (e.Delta <= 0 && canDown)) return;
        e.Handled = true;
        ForwardToParentScroller(scroller, e);
    }

    private static bool HasNestedScroller(ScrollViewer outer, MouseEventArgs e)
    {
        DependencyObject? node = outer.InputHitTest(e.GetPosition(outer)) as DependencyObject;
        while (node is not null && !ReferenceEquals(node, outer))
        {
            if (node is ScrollViewer) return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private static void ForwardToParentScroller(DependencyObject source, MouseWheelEventArgs e)
    {
        DependencyObject? node = source;
        while (node is not null)
        {
            node = VisualTreeHelper.GetParent(node);
            if (node is ScrollViewer parent)
            {
                parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
                {
                    RoutedEvent = UIElement.MouseWheelEvent,
                    Source = parent,
                });
                return;
            }
        }
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
            _provider.GetService<LoopController>()?.Dispose();
            _provider.GetService<MacroJobRunner>()?.Dispose();
            _provider.Dispose();
        }
        base.OnExit(e);
    }
}
