namespace PwHelper.Core.Ux;

/// <summary>
/// Firing countdown (M6): every game dispatch runs after an explicit
/// countdown so the user can look away from the game first.
/// </summary>
public static class Countdown
{
    public static async Task RunAsync(
        int seconds,
        IProgress<int>? remaining = null,
        CancellationToken cancellationToken = default)
    {
        if (seconds < 0) seconds = 0;
        for (int left = seconds; left > 0; left--)
        {
            remaining?.Report(left);
            await Task.Delay(1000, cancellationToken).ConfigureAwait(false);
        }
    }
}
