using PwAssistant.Core.Sync;

namespace PwAssistant.Core.Tests;

public sealed class FocusServiceTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();
    private static readonly Guid C = Guid.NewGuid();

    [Fact]
    public void CycleNext_WrapsAround_InGroupOrder()
    {
        var focus = new FocusService();
        focus.SetOrder([A, B, C]);

        Assert.Equal(A, focus.CycleNext());
        Assert.Equal(B, focus.CycleNext());
        Assert.Equal(C, focus.CycleNext());
        Assert.Equal(A, focus.CycleNext());
    }

    [Fact]
    public void ToggleLastTwo_SwapsBetweenTwo()
    {
        var focus = new FocusService();
        focus.SetOrder([A, B, C]);
        focus.SelectIndex(0);
        focus.SelectIndex(2);

        Assert.Equal(A, focus.ToggleLastTwo());
        Assert.Equal(C, focus.ToggleLastTwo());
    }

    [Fact]
    public void ToggleLastTwo_WithoutHistory_StaysOrStarts()
    {
        var focus = new FocusService();
        focus.SetOrder([A, B]);

        Assert.Equal(A, focus.ToggleLastTwo());
        Assert.Equal(A, focus.ToggleLastTwo());
    }

    [Fact]
    public void SelectIndex_OutOfRange_ReturnsNull()
    {
        var focus = new FocusService();
        focus.SetOrder([A]);

        Assert.Null(focus.SelectIndex(1));
        Assert.Null(focus.SelectIndex(-1));
    }

    [Fact]
    public void EmptyOrder_ReturnsNull()
    {
        var focus = new FocusService();

        Assert.Null(focus.CycleNext());
        Assert.Null(focus.ToggleLastTwo());
    }

    [Fact]
    public void SetOrder_ForgetsUnknownCurrent()
    {
        var focus = new FocusService();
        focus.SetOrder([A, B]);
        focus.SelectIndex(1);

        focus.SetOrder([A, C]);

        Assert.Equal(A, focus.CycleNext());
    }
}
