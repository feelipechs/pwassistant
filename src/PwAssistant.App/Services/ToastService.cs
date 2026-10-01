using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using PwAssistant.App.Views;

namespace PwAssistant.App.Services;

public enum ToastKind
{
    Info,
    Warn,
    Error,
}

/// <summary>One transient notification (auto-expires, never persisted).</summary>
public sealed record ToastItem(Guid Id, string Message, ToastKind Kind, DateTimeOffset At);

/// <summary>
/// Static notification bus: overlay toasts in Main/Group windows, tray
/// balloon fallback when no window is visible. Fire-and-forget from any
/// thread; UI work is marshaled. Secrets never pass through here.
/// </summary>
public static class ToastService
{
    private static readonly TimeSpan TimeToLive = TimeSpan.FromSeconds(4);
    private const int MaxItems = 3;

    private static DispatcherTimer? _sweeper;

    public static ObservableCollection<ToastItem> Items { get; } = new();

    /// <summary>Set once by MainWindow (owns the tray icon).</summary>
    public static Action<string, string>? BalloonSink { get; set; }

    public static void Show(string message, ToastKind kind = ToastKind.Info)
    {
        if (Application.Current is null) return;
        RunOnUi(() =>
        {
            EnsureSweeper();
            Items.Add(new ToastItem(Guid.NewGuid(), message, kind, DateTimeOffset.UtcNow));
            while (Items.Count > MaxItems)
                Items.RemoveAt(0);
            if (!AnyWindowVisible())
                BalloonSink?.Invoke(Resources.Strings.TrayTip, message);
        });
    }

    private static bool AnyWindowVisible()
    {
        try
        {
            return Application.Current.Windows
                .OfType<Window>()
                .Any(w => w.IsVisible && (w is MainWindow || w is GroupWindow));
        }
        catch
        {
            return true;
        }
    }

    private static void EnsureSweeper()
    {
        if (_sweeper is not null) return;
        _sweeper = new DispatcherTimer(
            TimeSpan.FromSeconds(1), DispatcherPriority.Background,
            (_, _) => Sweep(), Application.Current.Dispatcher);
        _sweeper.Start();
    }

    private static void Sweep()
    {
        DateTimeOffset cutoff = DateTimeOffset.UtcNow - TimeToLive;
        for (int i = Items.Count - 1; i >= 0; i--)
        {
            if (Items[i].At < cutoff)
                Items.RemoveAt(i);
        }
    }

    private static void RunOnUi(Action action)
    {
        System.Windows.Threading.Dispatcher? dispatcher = null;
        try
        {
            dispatcher = Application.Current?.Dispatcher;
        }
        catch
        {
            return;
        }
        if (dispatcher is null) return;
        if (dispatcher.CheckAccess())
            action();
        else
            // Fire-and-forget by contract: death handlers must never block
            // worker threads waiting on a busy UI.
            dispatcher.BeginInvoke(action);
    }
}
