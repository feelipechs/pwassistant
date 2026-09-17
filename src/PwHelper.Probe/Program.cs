using PwHelper.Core.Execution;
using PwHelper.Core.Input;
using PwHelper.Core.Models;
using PwHelper.Core.Sync;
using PwHelper.WinApi;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("PwHelper.Probe only runs on Windows (it drives the real game client).");
    return 2;
}

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("""
        PwHelper.Probe — M1/M3 validation console (run on Windows, game WITHOUT focus).
          probe key --pid <pid> --key F1 [--countdown 3] [--no-hygiene]
          probe click --pid <pid> --x <cx> --y <cy> [--countdown 3] [--no-hygiene]
          probe preset --pid <pid1> --pid <pid2> --x <cx> --y <cy> [--countdown 3] [--no-hygiene] [--sequential]
          probe sync --pid <pid1> --pid <pid2> [--no-hygiene]

        Client-area pixel coords for click. Observe the game visually.
        Preset (M3): key F1 on the first PID + UI-click on the second PID in one
        Simultaneous dispatch, plus a ghost offline account proving skip-without-abort.
        Sync (M4): installs the mouse hook and stays live until ENTER — a physical
        left-click inside ANY listed window replicates as a UI-click to the others.
        No fixed master; the focused window at click time is the master.
        """);
    return 0;
}

var options = Parse(args);
int countdown = options.TryGetValue("countdown", out string? cdText) && int.TryParse(cdText, out int cd) ? cd : 3;
bool hygiene = !options.ContainsKey("no-hygiene");

var resolver = new WindowResolver();
IInputStrategy strategy = new PostMessageBackgroundStrategy(applyHygiene: hygiene);

switch (args[0].ToLowerInvariant())
{
    case "key":
    {
        if (!options.TryGetValue("pid", out string? pidText) || !int.TryParse(pidText, out int pid))
        {
            Console.Error.WriteLine("Missing or invalid --pid.");
            return 2;
        }
        if (!options.TryGetValue("key", out string? key) || string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("Missing --key (e.g. F1).");
            return 2;
        }
        IntPtr hwnd = resolver.ResolveWindow(pid);
        if (hwnd == IntPtr.Zero)
        {
            Console.Error.WriteLine($"No visible window for PID {pid}.");
            return 1;
        }
        Console.WriteLine($"Target HWND=0x{hwnd.ToInt64():X} for PID {pid}.");
        await CountdownAsync(countdown);
        int vk = KeyMapper.Resolve(key);
        await strategy.SendKeyAsync(new ProbeTarget(Guid.Empty, hwnd), vk);
        Console.WriteLine($"Key {key} (VK=0x{vk:X}) posted. Did anything happen in the game?");
        break;
    }
    case "click":
    {
        if (!options.TryGetValue("pid", out string? pidText) || !int.TryParse(pidText, out int pid))
        {
            Console.Error.WriteLine("Missing or invalid --pid.");
            return 2;
        }
        if (!options.TryGetValue("x", out string? xText) || !int.TryParse(xText, out int x)
            || !options.TryGetValue("y", out string? yText) || !int.TryParse(yText, out int y))
        {
            Console.Error.WriteLine("Missing --x / --y client-area pixel coords.");
            return 2;
        }
        IntPtr hwnd = resolver.ResolveWindow(pid);
        if (hwnd == IntPtr.Zero)
        {
            Console.Error.WriteLine($"No visible window for PID {pid}.");
            return 1;
        }
        Console.WriteLine($"Target HWND=0x{hwnd.ToInt64():X} for PID {pid}.");
        await CountdownAsync(countdown);
        (int width, int height) = resolver.GetClientSize(hwnd);
        await strategy.SendUiClickAsync(new ProbeTarget(Guid.Empty, hwnd), (double)x / width, (double)y / height);
        Console.WriteLine($"Click ({x},{y}) posted. Did anything happen in the game?");
        break;
    }
    case "preset":
    {
        List<int> pids = CollectAll(args, "pid").Select(v => int.TryParse(v, out int p) ? p : -1).ToList();
        if (pids.Count < 2 || pids.Any(p => p <= 0))
        {
            Console.Error.WriteLine("Preset needs --pid <pid1> --pid <pid2> (repeat --pid per account).");
            return 2;
        }
        if (!options.TryGetValue("x", out string? xText) || !int.TryParse(xText, out int x)
            || !options.TryGetValue("y", out string? yText) || !int.TryParse(yText, out int y))
        {
            Console.Error.WriteLine("Missing --x / --y client-area pixel coords for the click action.");
            return 2;
        }
        bool sequential = options.ContainsKey("sequential");

        var accountIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var targets = new Dictionary<Guid, IWindowTarget>();
        for (int i = 0; i < 2; i++)
        {
            IntPtr hwnd = resolver.ResolveWindow(pids[i]);
            if (hwnd == IntPtr.Zero)
            {
                Console.Error.WriteLine($"No visible window for PID {pids[i]}.");
                return 1;
            }
            Console.WriteLine($"Target[{i}] HWND=0x{hwnd.ToInt64():X} for PID {pids[i]}.");
            targets[accountIds[i]] = new ProbeTarget(accountIds[i], hwnd);
        }

        (int clickW, int clickH) = resolver.GetClientSize(((ProbeTarget)targets[accountIds[1]]).WindowHandle);
        RelativePosition clickPos;
        try
        {
            clickPos = RelativePosition.FromAbsolute(x, y, clickW, clickH);
        }
        catch (ArgumentOutOfRangeException)
        {
            Console.Error.WriteLine($"Cannot convert click ({x},{y}): client size is {clickW}x{clickH}.");
            return 2;
        }
        if (!clickPos.IsValid)
        {
            Console.Error.WriteLine($"Click ({x},{y}) is outside client area {clickW}x{clickH}; refusing dispatch.");
            return 2;
        }

        var ghostId = Guid.NewGuid();
        var preset = new Preset
        {
            Name = "Probe M3",
            ExecutionMode = sequential ? ExecutionMode.Sequential : ExecutionMode.Simultaneous,
            Actions =
            [
                new AccountAction
                {
                    AccountId = accountIds[0],
                    Action = new GameAction { Type = ActionType.Key, Key = "F1" },
                },
                new AccountAction
                {
                    AccountId = accountIds[1],
                    Action = new GameAction { Type = ActionType.Click, RelativePosition = clickPos },
                },
                new AccountAction
                {
                    AccountId = ghostId,
                    Action = new GameAction { Type = ActionType.Key, Key = "F1" },
                },
            ],
        };

        await CountdownAsync(countdown);
        var executor = new MacroExecutor(strategy, id => targets.TryGetValue(id, out IWindowTarget? t) ? t : null);
        PresetExecutionResult result = await executor.ExecuteAsync(preset, Guid.NewGuid());

        Console.WriteLine($"Preset done ({preset.ExecutionMode}), canceled={result.Canceled}:");
        foreach (AccountExecutionResult r in result.Accounts)
        {
            string who = r.AccountId == ghostId ? "ghost (expected offline)" : $"PID {pids[accountIds.IndexOf(r.AccountId)]}";
            string status = r.Skipped ? $"SKIPPED ({r.Reason ?? r.Error})" : "OK";
            Console.WriteLine($"  {who}: {status}");
        }
        Console.WriteLine("Observe the game: F1 on account 1 + UI-click on account 2, ghost skipped.");
        break;
    }
    case "sync":
    {
        List<int> pids = CollectAll(args, "pid").Select(v => int.TryParse(v, out int p) ? p : -1).ToList();
        if (pids.Count < 2 || pids.Any(p => p <= 0))
        {
            Console.Error.WriteLine("Sync needs --pid <pid1> --pid <pid2> (repeat --pid per account).");
            return 2;
        }

        var accountIds = pids.Select(_ => Guid.NewGuid()).ToList();
        var targets = new Dictionary<Guid, (int Pid, IWindowTarget Target)>();
        for (int i = 0; i < pids.Count; i++)
        {
            IntPtr hwnd = resolver.ResolveWindow(pids[i]);
            if (hwnd == IntPtr.Zero)
            {
                Console.Error.WriteLine($"No visible window for PID {pids[i]}.");
                return 1;
            }
            Console.WriteLine($"Target[{i}] HWND=0x{hwnd.ToInt64():X} for PID {pids[i]}.");
            targets[accountIds[i]] = (pids[i], new ProbeTarget(accountIds[i], hwnd));
        }

        var sync = new SyncService();
        sync.SetEnabled(true);
        sync.SetSyncedAccounts(accountIds);

        using var hook = new MouseHook();
        hook.LeftButtonDown += click =>
        {
            try
            {
                foreach (Guid masterId in accountIds)
                {
                    (int masterPid, IWindowTarget masterTarget) = targets[masterId];
                    IntPtr masterHwnd = masterTarget.WindowHandle;
                    (int Width, int Height) size;
                    (int X, int Y) client;
                    try
                    {
                        size = resolver.GetClientSize(masterHwnd);
                        client = resolver.ScreenToClientPoint(masterHwnd, click.ScreenX, click.ScreenY);
                    }
                    catch (WinApiException)
                    {
                        continue;
                    }
                    if (client.X < 0 || client.Y < 0 || client.X > size.Width || client.Y > size.Height)
                        continue;

                    RelativePosition? fraction = sync.CaptureMasterClick(
                        client.X, client.Y, size.Width, size.Height, isLeftButton: true);
                    if (fraction is null)
                        return;

                    Console.WriteLine($"Master PID {masterPid} click ({client.X},{client.Y}) -> replicating...");
                    foreach (Guid replicaId in accountIds.Where(id => id != masterId))
                    {
                        (int replicaPid, IWindowTarget replicaTarget) = targets[replicaId];
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                (int Width, int Height) replicaSize =
                                    resolver.GetClientSize(replicaTarget.WindowHandle);
                                (int X, int Y) replicaPx =
                                    SyncService.ToReplicaPixels(fraction, replicaSize.Width, replicaSize.Height);
                                await strategy.SendUiClickAsync(replicaTarget, fraction.X, fraction.Y);
                                Console.WriteLine($"  replica PID {replicaPid} ({replicaPx.X},{replicaPx.Y}) OK");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"  replica PID {replicaPid} FAIL: {ex.Message}");
                            }
                        });
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Sync error: {ex.Message}");
            }
        };

        hook.Start();
        Console.WriteLine("Sync ON: left-click inside ANY listed game window replicates to the others.");
        Console.WriteLine("Press ENTER to stop.");
        using var cts = new CancellationTokenSource();
        _ = Task.Run(() =>
        {
            Console.ReadLine();
            cts.Cancel();
        });
        MessageLoop.RunUntilCancelled(cts.Token);
        hook.Stop();
        Console.WriteLine("Sync OFF.");
        break;
    }
    default:
        Console.Error.WriteLine($"Unknown command '{args[0]}'. Use 'key', 'click' or 'preset'.");
        return 2;
}

return 0;

static async Task CountdownAsync(int countdown)
{
    Console.WriteLine($"Firing in {countdown}s... keep the game UNFOCUSED, do not click it.");
    for (int left = countdown; left > 0; left--)
    {
        Console.WriteLine($"  {left}...");
        await Task.Delay(1000);
    }
}

static Dictionary<string, string> Parse(string[] args)
{
    var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (int i = 1; i < args.Length; i++)
    {
        string token = args[i];
        if (!token.StartsWith("--", StringComparison.Ordinal)) continue;
        string key = token[2..];
        string value = (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            ? args[++i]
            : "true";
        map[key] = value;
    }
    return map;
}

static List<string> CollectAll(string[] args, string name)
{
    var values = new List<string>();
    for (int i = 1; i < args.Length; i++)
    {
        if (!string.Equals(args[i], "--" + name, StringComparison.OrdinalIgnoreCase)) continue;
        if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            values.Add(args[++i]);
    }
    return values;
}

internal sealed record ProbeTarget(Guid AccountId, IntPtr WindowHandle) : IWindowTarget;
