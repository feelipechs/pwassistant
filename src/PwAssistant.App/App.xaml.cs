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
using Velopack;

namespace PwAssistant.App;

public partial class App : Application
{
    private ServiceProvider? _provider;

    /// <summary>
    /// Custom entry point so Velopack hooks run before any WPF overhead
    /// (install/update/uninstall fast-exits never build the app).
    /// Normal startup is unchanged: Run() raises OnStartup as usual.
    /// </summary>
    [STAThread]
    private static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }

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
        services.AddSingleton<ClientWindowMarker>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<AppUpdater>();
        services.AddSingleton(sp => new LoopController(
            sp.GetRequiredService<PresetDispatcher>(),
            sp.GetRequiredService<FileLogger>()));
        services.AddTransient<MainViewModel>();
        services.AddTransient<GroupViewModel>();
        services.AddTransient<MainWindow>();
        services.AddTransient<GroupWindow>();
        services.AddTransient<MiniWindow>();
        services.AddSingleton<Func<MiniWindow>>(sp => () => sp.GetRequiredService<MiniWindow>());
        services.AddTransient<PresetEditorControl>();

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
        // Popup-internal scrolling (e.g. ComboBox dropdowns): class handlers
        // fire inside popups too — never interfere there.
        if (IsInsidePopup(scroller)) return;
        // An open dropdown (separate Popup tree) owns the gesture.
        if (IsOverOpenDropdown()) return;
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

    /// <summary>True when the pointer sits inside an open Popup
    /// (e.g. a ComboBox dropdown): the Popup owns the wheel.</summary>
    private static bool IsOverOpenDropdown()
    {
        DependencyObject? node = Mouse.DirectlyOver as DependencyObject;
        while (node is not null)
        {
            if (node is System.Windows.Controls.Primitives.Popup)
                return true;
            node = LogicalTreeHelper.GetParent(node);
        }
        return false;
    }

    /// <summary>True when the scroller itself lives inside a Popup: class
    /// handlers fire there too, and must never steal the dropdown wheel.</summary>
    private static bool IsInsidePopup(DependencyObject node)
    {
        DependencyObject? current = node;
        while (current is not null)
        {
            if (current is System.Windows.Controls.Primitives.Popup)
                return true;
            current = VisualTreeHelper.GetParent(current) ?? LogicalTreeHelper.GetParent(current);
        }
        return false;
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
    /// Observed fire-and-forget: startup failures are logged, never lost.
    /// </summary>
    private async Task InitializeAndRegisterAsync(MainWindow main)
    {
        try
        {
            await main.ViewModel.InitializeAsync().ConfigureAwait(false);
            await main.Dispatcher.InvokeAsync(main.RegisterPresetHotkeys).Task.ConfigureAwait(false);
            // Observed fire-and-forget: the updater logs and never throws.
            _ = _provider?.GetService<AppUpdater>()?.CheckAndPromptAsync();
        }
        catch (Exception ex)
        {
            _provider?.GetService<FileLogger>()?.Error($"Startup init failed: {ex.Message}");
        }
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
