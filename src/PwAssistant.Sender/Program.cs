using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.WinApi;

// Separate sender process: one invocation per account per fire. Workload
// (JSON) arrives on stdin, diagnostics go to stdout, the outcome is the
// trailing RESULT line plus the exit code. This process never receives
// user input (no window, no hooks, no hotkeys), so its lone
// SetForegroundWindow is denied on normal machines: taskbar flashes,
// the screen never switches, the game still accepts the input (the
// Helper's background case). On lock-disabled machines the same call is
// granted and visibly switches — identical to the Helper there too.
//
// Exit codes mirror SenderExitCodes: 0 ok, 1 send error, 2 no window,
// 3 bad usage. stdout parsing lives in SenderCodec (unit-tested).

const int SettleMs = 50; // Helper parity: lone SFW, then sends.

if (!OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("PwAssistant.Sender only runs on Windows.");
    return SenderExitCodes.BadUsage;
}

if (args.Any(a => a is "-h" or "--help"))
{
    Console.WriteLine("""
        PwAssistant.Sender — per-account dispatch worker (spawned by the app, never run by hand).
          sender [--verbose] < workload.json
        Stdin: {"ProcessId":1234,"Verbose":true,"Actions":[{"Kind":"key","VirtualKey":113},{"Kind":"click","X":0.5,"Y":0.5,"Button":"left"},{"Kind":"sleep","Ms":250}]}
        Stdout: per-send [s]/[trace] lines, then "RESULT sent=N failed=M". Exit code: 0 ok, 1 send error, 2 no window, 3 bad usage.
        """);
    return SenderExitCodes.Ok;
}

bool verboseArg = false;
foreach (string arg in args)
{
    if (arg is "--verbose" or "-v")
        verboseArg = true;
    else
    {
        Console.Error.WriteLine($"Unknown argument '{arg}'. See --help.");
        return SenderExitCodes.BadUsage;
    }
}

SenderWorkload workload;
try
{
    string json = await Console.In.ReadToEndAsync();
    workload = SenderCodec.FromJson(json);
}
catch (Exception ex) when (ex is ArgumentException || ex is System.Text.Json.JsonException)
{
    Console.Error.WriteLine($"Bad workload: {ex.Message}");
    return SenderExitCodes.BadUsage;
}

bool verbose = verboseArg || workload.Verbose;
var resolver = new WindowResolver();
IntPtr hwnd = resolver.ResolveWindow(workload.ProcessId);
if (hwnd == IntPtr.Zero)
{
    Console.WriteLine($"ERROR no-window pid={workload.ProcessId}");
    Console.WriteLine("RESULT sent=0 failed=1");
    return SenderExitCodes.NoWindow;
}

var strategy = new PostMessageBackgroundStrategy();
if (verbose)
{
    strategy.Traced += trace => Console.WriteLine(
        $"  [trace] hwnd=0x{trace.Hwnd:X} {trace.Tag} msg=0x{trace.Message:X} ok={(trace.Ok ? 1 : 0)} win32={trace.Win32Error}");
}

try
{
    if (WindowFocus.IsMinimized(hwnd) && WindowFocus.RestoreIfMinimized(hwnd) && verbose)
        Console.WriteLine($"  [sender] hwnd=0x{hwnd:X} restored-minimized");

    IntPtr prev = resolver.GetForegroundWindow();
    bool granted = WindowFocus.TrySetSoft(hwnd);
    GuiState gui = WindowDiagnostics.GetGuiState(hwnd);
    int pumpMs = WindowDiagnostics.ProbePumpMs(hwnd);
    Console.WriteLine($"  [s] hwnd=0x{hwnd:X} setfg={(granted ? 1 : 0)}"
        + $" gui=0x{gui.ActiveWindow:X} focus=0x{gui.FocusWindow:X} pumpMs={pumpMs}");
    if (verbose)
        Console.WriteLine($"  [sender] prev=0x{prev:X} pid={workload.ProcessId}");

    await Task.Delay(SettleMs);

    var target = new DirectTarget(hwnd);
    int sent = 0;
    try
    {
        foreach (SenderAction action in workload.Actions)
        {
            switch (action.Kind?.ToLowerInvariant())
            {
                case "key":
                    await strategy.SendKeyDirectAsync(target, action.VirtualKey);
                    sent++;
                    break;
                case "click":
                    await strategy.SendUiClickCleanAsync(target, action.X, action.Y, ParseButton(action.Button));
                    sent++;
                    break;
                case "sleep":
                    if (action.Ms > 0)
                        await Task.Delay(action.Ms);
                    break;
                default:
                    throw new WinApiException($"Unknown action kind '{action.Kind}'.");
            }
        }

        Console.WriteLine($"RESULT sent={sent} failed=0");
        return SenderExitCodes.Ok;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR {ex.Message}");
        Console.WriteLine($"RESULT sent={sent} failed=1");
        return SenderExitCodes.SendError;
    }
}
catch (Exception ex)
{
    // Resolve/restore/probe section: never expected to throw (contracts say
    // so), but a worker must never dump a stack where RESULT is parsed.
    Console.WriteLine($"ERROR {ex.Message}");
    Console.WriteLine("RESULT sent=0 failed=1");
    return SenderExitCodes.SendError;
}

static MouseButton ParseButton(string? button) => button?.ToLowerInvariant() switch
{
    "right" => MouseButton.Right,
    "left" or null or "" => MouseButton.Left,
    _ => throw new WinApiException($"Unknown button '{button}'."),
};

internal sealed record DirectTarget(IntPtr WindowHandle) : IWindowTarget
{
    public Guid AccountId => Guid.Empty; // attribution happens app-side
}
