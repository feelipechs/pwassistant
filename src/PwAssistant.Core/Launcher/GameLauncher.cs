using System.Diagnostics;
using System.IO;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;

namespace PwAssistant.Core.Launcher;

/// <summary>Live session spawned by the launcher for one account.</summary>
public sealed record GameSession(Guid AccountId, int ProcessId, DateTimeOffset StartedAt);

/// <summary>
/// Per-account launch shortcut (target + args + working dir + icon).
/// The taskbar associates each client with its own shortcut file,
/// yielding one button per account (proven by manual experiment).
/// Pure data — the App layer materializes the file via WinApi.
/// </summary>
public sealed record ShortcutDefinition(
    string ShortcutPath,
    string TargetPath,
    string Arguments,
    string WorkingDirectory,
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

    public static string IconDirectory() => Path.Combine(
        AppContext.BaseDirectory, "Resources", "Classes");

    public static ShortcutDefinition BuildShortcutDefinition(
        Server server, Account account, string plaintextPassword,
        string clientsDirectory, string iconDirectory)
    {
        ProcessStartInfo direct = BuildStartInfo(server, account, plaintextPassword);
        return WithIcon(server, account, clientsDirectory, iconDirectory,
            direct.Arguments, direct.WorkingDirectory);
    }

    /// <summary>
    /// Identity-only shortcut: same path/target/workdir/icon, but NO
    /// arguments — hence no secret on disk. This is the resting state of
    /// every .lnk file. Credentials exist on disk only in the brief window
    /// between materializing the full shortcut and scrubbing it right
    /// after Process.Start (see LaunchAsync), plus crash residue cleaned
    /// by CleanseShortcuts at startup.
    /// </summary>
    public static ShortcutDefinition BuildIdentityShortcutDefinition(
        Server server, Account account, string clientsDirectory, string iconDirectory)
    {
        string workingDirectory = string.IsNullOrWhiteSpace(server.ElementClientPath)
            ? string.Empty
            : Path.GetDirectoryName(server.ElementClientPath) ?? string.Empty;
        return WithIcon(server, account, clientsDirectory, iconDirectory,
            arguments: string.Empty, workingDirectory);
    }

    private static ShortcutDefinition WithIcon(
        Server server, Account account, string clientsDirectory, string iconDirectory,
        string arguments, string workingDirectory)
    {
        string icon = ClassCatalog.TryGet(account.Class, out ClassInfo info)
            ? Path.Combine(iconDirectory, info.Key + ".ico")
            : server.ElementClientPath;
        return new ShortcutDefinition(
            ShortcutPath: Path.Combine(clientsDirectory, $"{account.Id:N}.lnk"),
            TargetPath: server.ElementClientPath,
            Arguments: arguments,
            WorkingDirectory: workingDirectory,
            IconLocation: icon);
    }

    public static ProcessStartInfo BuildShortcutStartInfo(ShortcutDefinition definition) =>
        new()
        {
            // .lnk execution requires the shell: working dir and icon come
            // from the shortcut file itself.
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
        ShortcutDefinition? fullDefinition = null;
        if (_ensureShortcut is null)
        {
            startInfo = BuildStartInfo(server, account, plaintextPassword);
        }
        else
        {
            // The shell resolves the .lnk at Process.Start time, so the
            // credentials only need to be on disk for that instant: write
            // the full shortcut, start, then scrub it back to identity.
            fullDefinition = BuildShortcutDefinition(
                server, account, plaintextPassword, DefaultClientsDirectory(), IconDirectory());
            _ensureShortcut(fullDefinition);
            startInfo = BuildShortcutStartInfo(fullDefinition);
        }
        try
        {
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
        finally
        {
            // Scrub even when the launch fails: no secret rests on disk.
            if (fullDefinition is not null)
                _ensureShortcut!(BuildIdentityShortcutDefinition(
                    server, account, DefaultClientsDirectory(), IconDirectory()));
        }
    }

    /// <summary>
    /// Startup hygiene: rewrites the identity (secret-free) shortcut for
    /// every known account — cleansing crash residue from an interrupted
    /// launch — and deletes orphan .lnk files with no matching account.
    /// Best effort per file; returns the number of files fixed. Never
    /// touches files outside the {guid:N}.lnk naming pattern.
    /// </summary>
    public int CleanseAllShortcuts(IEnumerable<Server> servers)
    {
        if (_ensureShortcut is null)
            return 0;
        return CleanseShortcuts(servers, _ensureShortcut,
            DefaultClientsDirectory(), IconDirectory());
    }

    public static int CleanseShortcuts(
        IEnumerable<Server> servers,
        Action<ShortcutDefinition> ensure,
        string clientsDirectory,
        string iconDirectory)
    {
        ArgumentNullException.ThrowIfNull(ensure);
        int fixed_ = 0;
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Server server in servers)
        {
            foreach (Account account in server.Accounts)
            {
                known.Add($"{account.Id:N}.lnk");
                try
                {
                    ensure(BuildIdentityShortcutDefinition(
                        server, account, clientsDirectory, iconDirectory));
                    fixed_++;
                }
                catch
                {
                    // Best effort: one bad shortcut never blocks the rest.
                }
            }
        }
        string[] orphans;
        try
        {
            if (!Directory.Exists(clientsDirectory))
                return fixed_;
            orphans = Directory.GetFiles(clientsDirectory, "*.lnk");
        }
        catch
        {
            return fixed_;
        }
        foreach (string orphan in orphans)
        {
            if (known.Contains(Path.GetFileName(orphan)))
                continue;
            // Only our own naming pattern — never delete foreign files.
            string name = Path.GetFileNameWithoutExtension(orphan);
            if (name.Length != 32 || !Guid.TryParseExact(name, "N", out _))
                continue;
            try
            {
                File.Delete(orphan);
                fixed_++;
            }
            catch
            {
                // Best effort.
            }
        }
        return fixed_;
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
