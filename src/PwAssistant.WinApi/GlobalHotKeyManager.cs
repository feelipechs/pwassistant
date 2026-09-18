namespace PwAssistant.WinApi;

/// <summary>Global preset hotkey via RegisterHotKey (M6). Window-handle bound; App-owned.</summary>
public sealed class GlobalHotKeyManager : IDisposable
{
    private readonly IntPtr _windowHandle;
    private int _nextId = 1;
    private readonly Dictionary<int, Action> _handlers = new();
    private bool _disposed;

    public const int WM_HOTKEY = 0x0312;

    public GlobalHotKeyManager(IntPtr windowHandle)
    {
        _windowHandle = windowHandle;
    }

    public int Register(uint modifiers, uint virtualKey, Action handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        int id = _nextId++;
        if (!NativeMethods.RegisterHotKey(_windowHandle, id, modifiers, virtualKey))
            throw new WinApiException("RegisterHotKey failed.");
        _handlers[id] = handler;
        return id;
    }

    /// <summary>Route a received WM_HOTKEY message id to its handler. Returns false if unknown.</summary>
    public bool Dispatch(int id)
    {
        if (_handlers.TryGetValue(id, out Action? handler))
        {
            handler();
            return true;
        }
        return false;
    }

    public void Unregister(int id)
    {
        NativeMethods.UnregisterHotKey(_windowHandle, id);
        _handlers.Remove(id);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        foreach (int id in _handlers.Keys.ToArray())
            NativeMethods.UnregisterHotKey(_windowHandle, id);
        _handlers.Clear();
        GC.SuppressFinalize(this);
    }
}
