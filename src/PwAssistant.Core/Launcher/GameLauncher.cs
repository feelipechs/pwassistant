using System.Diagnostics;
using System.IO;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;

namespace PwAssistant.Core.Launcher;

/// <summary>Live session spawned by the launcher for one account.</summary>
public sealed record GameSession(Guid AccountId, int ProcessId, DateTimeOffset StartedAt);

/// <summary>
/// Spawns elementclient.exe with startbypatcher credentials and polls for
/// the game window (timeout-based, never a fixed Sleep). Window titles may
/// vary with role — display only, never a lookup key (key is PID → HWND).
/// </summary>
public sealed class GameLauncher
{
    private readonly IWindowResolver _resolver;

    public GameLauncher(IWindowResolver resolver)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
    }

    public static ProcessStartInfo BuildStartInfo(Server server, Account account, string plaintextPassword)
    {
        if (string.IsNullOrWhiteSpace(server.ElementClientPath))
            throw new ArgumentException("Server has no element client path.", nameof(server));

        var arguments = $"startbypatcher user:{account.Login} pwd:{plaintextPassword} role:{account.Role}";
        if (!string.IsNullOrWhiteSpace(server.ForceServer))
            arguments += $" {server.ForceServer}";

        return new ProcessStartInfo
        {
            FileName = server.ElementClientPath,
            Arguments = arguments,
            // The client resolves configs.pck and other assets relatively:
            // it must start with its own folder as the working directory.
            WorkingDirectory = Path.GetDirectoryName(server.ElementClientPath) ?? string.Empty,
            UseShellExecute = false
        };
    }

    public async Task<GameSession> LaunchAsync(
        Server server,
        Account account,
        string plaintextPassword,
        TimeSpan windowTimeout,
        CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo = BuildStartInfo(server, account, plaintextPassword);
        Process? process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the game client.");

        try
        {
            IntPtr hwnd = await WaitForWindowAsync(
                process.Id, windowTimeout, cancellationToken).ConfigureAwait(false);

            account.ProcessId = process.Id;
            account.WindowHandle = hwnd;
            return new GameSession(account.Id, process.Id, DateTimeOffset.UtcNow);
        }
        catch
        {
            account.ProcessId = null;
            account.WindowHandle = IntPtr.Zero;
            throw;
        }
    }

    public async Task<IntPtr> WaitForWindowAsync(
        int processId, TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IntPtr hwnd = _resolver.ResolveWindow(processId);
            if (hwnd != IntPtr.Zero && _resolver.IsWindowAlive(hwnd))
                return hwnd;
            await Task.Delay(250, cancellationToken).ConfigureAwait(false);
        }
        throw new TimeoutException($"Game window for PID {processId} did not appear in time.");
    }
}
