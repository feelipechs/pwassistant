using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Sync;
using PwAssistant.WinApi;

namespace PwAssistant.App.Services;

/// <summary>
/// Window-focus switching (B4): backtick cycles online members, Shift-tap
/// toggles the last two, numpad selects by group position. Explicit
/// user-requested focus only; everything else stays focus-free.
/// </summary>
public sealed class FocusController : IDisposable
{
    private const int VK_SHIFT = 0x10;
    private const int VK_LSHIFT = 0xA0;
    private const int VK_RSHIFT = 0xA1;
    private const int VK_OEM_3 = 0xC0;
    private const int VK_NUMPAD0 = 0x60;
    private const int VK_NUMPAD9 = 0x69;
    private const int ShiftTapMs = 300;

    private readonly KeyboardHook _hook;
    private readonly FocusService _focus;
    private readonly AppState _state;
    private readonly IWindowResolver _resolver;
    private bool _disposed;

    private DateTimeOffset? _shiftDownAt;
    private bool _shiftTainted;

    public FocusController(
        KeyboardHook hook, FocusService focus, AppState state, IWindowResolver resolver)
    {
        _hook = hook;
        _focus = focus;
        _state = state;
        _resolver = resolver;
        _hook.KeyTransition += OnKeyTransition;
    }

    public bool Enabled { get; set; }

    public void Start() => _hook.Start();
    public void Stop() => _hook.Stop();

    public void SetOrder(IEnumerable<Guid> accountIds) => _focus.SetOrder(accountIds);

    private void OnKeyTransition(KeyTransition transition)
    {
        if (!Enabled) return;

        if (IsShift(transition.VirtualKey))
        {
            if (transition.IsKeyDown)
            {
                _shiftDownAt = DateTimeOffset.UtcNow;
                _shiftTainted = false;
            }
            else if (_shiftDownAt.HasValue
                && !_shiftTainted
                && (DateTimeOffset.UtcNow - _shiftDownAt.Value).TotalMilliseconds < ShiftTapMs)
            {
                Focus(_focus.ToggleLastTwo());
            }
            if (!transition.IsKeyDown)
                _shiftDownAt = null;
            return;
        }

        if (!transition.IsKeyDown) return;
        _shiftTainted = true;

        if (transition.VirtualKey == VK_OEM_3)
            Focus(_focus.CycleNext());
        else if (transition.VirtualKey is >= VK_NUMPAD0 and <= VK_NUMPAD9)
            Focus(_focus.SelectIndex(NumpadIndex(transition.VirtualKey)));
    }

    private static bool IsShift(int vk) => vk is VK_SHIFT or VK_LSHIFT or VK_RSHIFT;

    /// <summary>Numpad 1-9 = positions 0-8, numpad 0 = position 9.</summary>
    private static int NumpadIndex(int vk) => vk == VK_NUMPAD0 ? 9 : vk - VK_NUMPAD0 - 1;

    private void Focus(Guid? accountId)
    {
        if (accountId is null) return;
        Account? account = _state.Data.Servers
            .SelectMany(s => s.Accounts)
            .FirstOrDefault(a => a.Id == accountId
                && a.WindowHandle != IntPtr.Zero
                && _resolver.IsWindowAlive(a.WindowHandle));
        if (account is null) return;
        WindowFocus.BringToFront(account.WindowHandle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _hook.KeyTransition -= OnKeyTransition;
        _hook.Dispose();
        GC.SuppressFinalize(this);
    }
}
