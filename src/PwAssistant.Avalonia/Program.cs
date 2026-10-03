using Avalonia;
using PwAssistant.Core.Ux;
using Velopack;

namespace PwAssistant.Avalonia;

internal static class Program
{
    /// <summary>
    /// Custom entry point so Velopack hooks run before any UI overhead
    /// (install/update/uninstall fast-exits never build the app).
    /// Normal startup is unchanged: StartWithClassicDesktopLifetime raises
    /// OnFrameworkInitializationCompleted as usual.
    /// </summary>
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        // Single instance AFTER the Velopack lifecycle commands above (those
        // must never hit this gate). Updater restarts can briefly overlap the
        // exiting copy, so a failed first attempt waits ~10 s before giving up.
        SingleInstance? instance = SingleInstance.TryAcquire(@"Local\PwAssistant.SingleInstance");
        DateTimeOffset started = DateTimeOffset.UtcNow;
        while (instance is null && DateTimeOffset.UtcNow - started < TimeSpan.FromSeconds(10))
        {
            Thread.Sleep(500);
            instance = SingleInstance.TryAcquire(@"Local\PwAssistant.SingleInstance");
        }
        if (instance is null)
        {
            // No window exists yet to parent a dialog (a real dialog comes in F3).
            Console.WriteLine("O PwAssistant já está em execução.");
            return;
        }
        using (instance)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
