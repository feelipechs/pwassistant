namespace PwAssistant.Avalonia;

/// <summary>
/// Process-teardown gate. Avalonia's Application.Current stays non-null
/// during shutdown (unlike WPF's Dispatcher.HasShutdownStarted), so
/// hide-cache windows (Group) must check this instead — otherwise they
/// cancel their own close forever and Shutdown() never completes
/// (the app "hangs" on tray Exit with hooks still installed).
/// Set once from ShutdownRequested; never reset.
/// </summary>
public static class AppShutdown
{
    public static bool Requested { get; private set; }

    public static void Request() => Requested = true;
}
