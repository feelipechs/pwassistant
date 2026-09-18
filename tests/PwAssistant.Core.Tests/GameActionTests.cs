using PwAssistant.Core.Models;

namespace PwAssistant.Core.Tests;

public sealed class GameActionTests
{
    [Fact]
    public void NegativeDelay_IsNormalizedToZero()
    {
        var action = new GameAction { Type = ActionType.Key, Key = "F1", DelayBeforeMs = -50 };
        Assert.Equal(0, action.DelayBeforeMs);
    }

    [Fact]
    public void Click_ButtonDefaultsToLeft()
    {
        var action = new GameAction { Type = ActionType.Click };
        Assert.Equal(MouseButton.Left, action.Button);
    }

    [Fact]
    public void Click_WithoutPosition_IsInvalid()
    {
        var action = new GameAction { Type = ActionType.Click };
        Assert.Contains(action.Validate(), e => e.Contains("position", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Click_WithOutOfRangePosition_IsInvalid()
    {
        var action = new GameAction
        {
            Type = ActionType.Click,
            RelativePosition = new RelativePosition(1.5, 0.5)
        };
        Assert.NotEmpty(action.Validate());
    }

    [Fact]
    public void ValidClick_HasNoErrors()
    {
        var action = new GameAction
        {
            Type = ActionType.Click,
            RelativePosition = new RelativePosition(0.5, 0.5)
        };
        Assert.Empty(action.Validate());
    }

    [Fact]
    public void Key_WithoutName_IsInvalid()
    {
        var action = new GameAction { Type = ActionType.Key };
        Assert.NotEmpty(action.Validate());
    }

    [Theory]
    [InlineData(0.25, 0.5, 800, 600, 200, 300)]
    [InlineData(1.0, 1.0, 1024, 768, 1024, 768)]
    public void Fraction_ConvertsToAbsolutePixels(
        double fx, double fy, int w, int h, int expectedX, int expectedY)
    {
        var position = new RelativePosition(fx, fy);
        Assert.Equal((expectedX, expectedY), position.ToAbsolute(w, h));
    }

    [Fact]
    public void FromAbsolute_RoundTrips()
    {
        var position = RelativePosition.FromAbsolute(200, 150, 800, 600);
        Assert.Equal(0.25, position.X, precision: 5);
        Assert.Equal(0.25, position.Y, precision: 5);
    }
}
