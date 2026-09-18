namespace PwHelper.Core.Sync;

/// <summary>
/// Window-focus order logic (B4): cycle forward, toggle last two, select by
/// position. Pure logic over an ordered member list; HWND resolution and
/// focusing are owned by the App layer. Unknown ids are forgotten.
/// </summary>
public sealed class FocusService
{
    private List<Guid> _order = new();
    private Guid? _current;
    private Guid? _previous;

    public void SetOrder(IEnumerable<Guid> accountIds)
    {
        _order = new List<Guid>(accountIds);
        if (_current is not { } current || !_order.Contains(current))
        {
            _current = null;
            _previous = null;
        }
        else if (_previous is not { } previous || !_order.Contains(previous))
        {
            _previous = null;
        }
    }

    public IReadOnlyList<Guid> Order => _order;

    /// <summary>Next member after the current one, wrapping around.</summary>
    public Guid? CycleNext()
    {
        if (_order.Count == 0) return null;
        int index = _current is { } current ? _order.IndexOf(current) : -1;
        return MoveTo(_order[(index + 1) % _order.Count]);
    }

    /// <summary>Swap between the last two focused members.</summary>
    public Guid? ToggleLastTwo()
    {
        if (_previous is null) return _current ?? (_order.Count > 0 ? MoveTo(_order[0]) : null);
        (_current, _previous) = (_previous, _current);
        return _current;
    }

    /// <summary>Member at a zero-based position (numpad 1 = 0).</summary>
    public Guid? SelectIndex(int index)
    {
        if (index < 0 || index >= _order.Count) return null;
        return MoveTo(_order[index]);
    }

    private Guid MoveTo(Guid id)
    {
        if (_current != id)
        {
            _previous = _current;
            _current = id;
        }
        return id;
    }
}
