namespace PwAssistant.Core.Input;

using PwAssistant.Core.Models;

/// <summary>Where to send input: the live window of one specific account.</summary>
public interface IWindowTarget
{
    Guid AccountId { get; }
    IntPtr WindowHandle { get; }
}

/// <summary>
/// How to send input. The v1 implementation is PostMessage-with-priming
/// (no focus, no driver, no injection). Kept behind this interface so the
/// strategy can change without touching the rest of the app.
/// </summary>
public interface IInputStrategy
{
    Task SendKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default);
    Task SendUiClickAsync(
        IWindowTarget target, double relativeX, double relativeY,
        MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default);

    /// <summary>
    /// Ordered wait scoped to one account's sequence, so batching
    /// strategies can keep delays (DelayBeforeMs, repeat intervals) inside
    /// the batch in exact order instead of collapsing them. Default is the
    /// legacy behavior: wait in-process.
    /// </summary>
    Task SleepAsync(IWindowTarget target, int millisecondsDelay, CancellationToken cancellationToken = default) =>
        Task.Delay(millisecondsDelay, cancellationToken);

    /// <summary>
    /// Flush one account's buffered actions (no-op unless the strategy
    /// batches). Throwing here attributes the failure to that account
    /// through the normal skip-with-error path.
    /// </summary>
    Task FlushAsync(IWindowTarget target, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;
}

/// <summary>Resolves the live top-level game window for a process id.</summary>
public interface IWindowResolver
{
    IntPtr ResolveWindow(int processId);
    bool IsWindowAlive(IntPtr windowHandle);
    (int Width, int Height) GetClientSize(IntPtr windowHandle);
    (int X, int Y) ScreenToClientPoint(IntPtr windowHandle, int screenX, int screenY);
    /// <summary>Topmost window at a screen point (Z-order aware).</summary>
    IntPtr ResolveTopWindowAtPoint(int screenX, int screenY);
    /// <summary>Current foreground window (for active-member highlight).</summary>
    IntPtr GetForegroundWindow();
}
