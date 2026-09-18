namespace PwAssistant.Core.Models;

/// <summary>
/// Click position as a fraction of the game client area (0.0-1.0 on each
/// axis). Fractions — never absolute pixels — so presets keep working when
/// game windows have different sizes or positions.
/// </summary>
public sealed record RelativePosition(double X, double Y)
{
    public bool IsValid => X is >= 0 and <= 1 && Y is >= 0 and <= 1;

    public (int X, int Y) ToAbsolute(int clientWidth, int clientHeight) =>
        ((int)(X * clientWidth), (int)(Y * clientHeight));

    public static RelativePosition FromAbsolute(int x, int y, int clientWidth, int clientHeight)
    {
        if (clientWidth <= 0) throw new ArgumentOutOfRangeException(nameof(clientWidth));
        if (clientHeight <= 0) throw new ArgumentOutOfRangeException(nameof(clientHeight));
        return new RelativePosition((double)x / clientWidth, (double)y / clientHeight);
    }
}

/// <summary>Optional combo/loop attached to an action.</summary>
public sealed record RepeatSettings(int Times, int IntervalMs)
{
    public int NormalizedTimes => Math.Max(1, Times);
    public int NormalizedIntervalMs => Math.Max(0, IntervalMs);
}
