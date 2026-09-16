using PwHelper.Core.Input;
using PwHelper.WinApi;

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("PwHelper.Probe only runs on Windows (it drives the real game client).");
    return 2;
}

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine("""
        PwHelper.Probe — M1 validation console (run on Windows, game WITHOUT focus).
          probe key --pid <pid> --key F1 [--countdown 3] [--no-hygiene]
          probe click --pid <pid> --x <cx> --y <cy> [--countdown 3] [--no-hygiene]

        Client-area pixel coords for click. Observe the game visually.
        """);
    return 0;
}

var options = Parse(args);
if (!options.TryGetValue("pid", out string? pidText) || !int.TryParse(pidText, out int pid))
{
    Console.Error.WriteLine("Missing or invalid --pid.");
    return 2;
}

int countdown = options.TryGetValue("countdown", out string? cdText) && int.TryParse(cdText, out int cd) ? cd : 3;
bool hygiene = !options.ContainsKey("no-hygiene");

var resolver = new WindowResolver();
IntPtr hwnd = resolver.ResolveWindow(pid);
if (hwnd == IntPtr.Zero)
{
    Console.Error.WriteLine($"No visible window for PID {pid}.");
    return 1;
}
Console.WriteLine($"Target HWND=0x{hwnd.ToInt64():X} for PID {pid}.");

Console.WriteLine($"Firing in {countdown}s... keep the game UNFOCUSED, do not click it.");
for (int left = countdown; left > 0; left--)
{
    Console.WriteLine($"  {left}...");
    await Task.Delay(1000);
}

IInputStrategy strategy = new PostMessageBackgroundStrategy(applyHygiene: hygiene);
var target = new ProbeTarget(Guid.Empty, hwnd);

switch (args[0].ToLowerInvariant())
{
    case "key":
    {
        if (!options.TryGetValue("key", out string? key) || string.IsNullOrWhiteSpace(key))
        {
            Console.Error.WriteLine("Missing --key (e.g. F1).");
            return 2;
        }
        int vk = KeyMapper.Resolve(key);
        await strategy.SendKeyAsync(target, vk);
        Console.WriteLine($"Key {key} (VK=0x{vk:X}) posted. Did anything happen in the game?");
        break;
    }
    case "click":
    {
        if (!options.TryGetValue("x", out string? xText) || !int.TryParse(xText, out int x)
            || !options.TryGetValue("y", out string? yText) || !int.TryParse(yText, out int y))
        {
            Console.Error.WriteLine("Missing --x / --y client-area pixel coords.");
            return 2;
        }
        (int width, int height) = resolver.GetClientSize(hwnd);
        await strategy.SendUiClickAsync(target, (double)x / width, (double)y / height);
        Console.WriteLine($"Click ({x},{y}) posted. Did anything happen in the game?");
        break;
    }
    default:
        Console.Error.WriteLine($"Unknown command '{args[0]}'. Use 'key' or 'click'.");
        return 2;
}

return 0;

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

internal sealed record ProbeTarget(Guid AccountId, IntPtr WindowHandle) : IWindowTarget;
