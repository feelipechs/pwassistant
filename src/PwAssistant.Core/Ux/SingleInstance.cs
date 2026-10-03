namespace PwAssistant.Core.Ux;

/// <summary>
/// Single-instance guard: at most one app copy runs per session, so two
/// copies never fight over global hooks (double hotkeys/dispatch) or the
/// shared accounts file (last-writer-wins data loss). A named system mutex
/// (`Local\` scope = per Windows session); a crashed holder abandons it
/// and the next acquirer transparently owns it (framework contract).
/// Pure BCL (no P/Invoke): blocking waits use sleeps, never hooks.
/// </summary>
public sealed class SingleInstance : IDisposable
{
    private readonly Mutex _mutex;
    private bool _disposed;

    private SingleInstance(Mutex mutex)
    {
        _mutex = mutex;
    }

    /// <summary>
    /// Tries to become the single instance. Returns null when another live
    /// copy already holds the name. Never throws for contention (null);
    /// throws only for real errors (nor for abandonment: an abandoned
    /// mutex is acquired normally).
    /// </summary>
    public static SingleInstance? TryAcquire(string name)
    {
        ArgumentNullException.ThrowIfNull(name);
        Mutex mutex;
        try
        {
            mutex = new Mutex(initiallyOwned: false, name);
        }
        catch
        {
            return null;
        }

        bool owned;
        try
        {
            owned = mutex.WaitOne(0);
        }
        catch (AbandonedMutexException)
        {
            owned = true;
        }
        catch
        {
            mutex.Dispose();
            return null;
        }

        if (!owned)
        {
            mutex.Dispose();
            return null;
        }
        return new SingleInstance(mutex);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        try
        {
            _mutex.ReleaseMutex();
        }
        catch
        {
            // Not owned (abandoned path quirks, double dispose): never throw.
        }
        _mutex.Dispose();
        GC.SuppressFinalize(this);
    }
}
