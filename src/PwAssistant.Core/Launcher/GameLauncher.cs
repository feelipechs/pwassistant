using System.Diagnostics;
using System.IO;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;

namespace PwAssistant.Core.Launcher;

/// <summary>Live session spawned by the launcher for one account.</summary>
public sealed record GameSession(Guid AccountId, int ProcessId, DateTimeOffset StartedAt);

/// <summary>
/// Per-account shortcut definition. Launching through a .lnk with a
/// distinct AppUserModelID gives each client its own taskbar button.
/// Pure data — the App layer materializes the file via WinApi.
/// </summary>
public sealed record ShortcutDefinition(
    string ShortcutPath,
    string TargetPath,
    string Arguments,
    string WorkingDirectory,
    string AppUserModelId,
    string IconLocation);

/// <summary>
/// Spawns elementclient.exe with startbypatcher credentials and polls for
/// the game window (timeout-based, never a fixed Sleep). Window titles may
/// vary with role — display only, never a lookup key (key is PID → HWND).
/// </summary>
public sealed class GameLauncher
{
    private readonly IWindowResolver _resolver;
    private readonly Action<ShortcutDefinition>? _ensureShortcut;

    public GameLauncher(IWindowResolver resolver, Action<ShortcutDefinition>? ensureShortcut = null)
    {
        _resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        _ensureShortcut = ensureShortcut;
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

    public static string DefaultClientsDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "PwAssistant", "clients");

    public static ShortcutDefinition BuildShortcutDefinition(
        Server server, Account account, string plaintextPassword, string clientsDirectory)
    {
        ProcessStartInfo direct = BuildStartInfo(server, account, plaintextPassword);
        return new ShortcutDefinition(
            ShortcutPath: Path.Combine(clientsDirectory, $"{account.Id:N}.lnk"),
            TargetPath: server.ElementClientPath,
            Arguments: direct.Arguments,
            WorkingDirectory: direct.WorkingDirectory,
            AppUserModelId: $"PwAssistant.Client.{account.Id:N}",
            IconLocation: server.ElementClientPath);
    }

    public static ProcessStartInfo BuildShortcutStartInfo(ShortcutDefinition definition) =>
        new()
        {
            // .lnk execution requires the shell: WorkingDirectory and icon
            // come from the shortcut file itself.
            FileName = definition.ShortcutPath,
            UseShellExecute = true,
        };

    public async Task<GameSession> LaunchAsync(
        Server server,
        Account account,
        string plaintextPassword,
        TimeSpan windowTimeout,
        CancellationToken cancellationToken = default)
    {
        ProcessStartInfo startInfo;
        if (_ensureShortcut is null)
        {
            startInfo = BuildStartInfo(server, account, plaintextPassword);
        }
        else
        {
            ShortcutDefinition definition = BuildShortcutDefinition(
                server, account, plaintextPassword, DefaultClientsDirectory());
            _ensureShortcut(definition);
            startInfo = BuildShortcutStartInfo(definition);
        }
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
