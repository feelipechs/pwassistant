namespace PwHelper.Core.Input;

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
    Task SendUiClickAsync(IWindowTarget target, double relativeX, double relativeY, CancellationToken cancellationToken = default);
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
}
