using System.Text.Json;

namespace PwAssistant.Core.Input;

/// <summary>
/// Wire contract between the app and the separate sender process
/// (PwAssistant.Sender): one workload per account on stdin (JSON), one
/// RESULT line on stdout, process exit code for the outcome. The sender
/// never receives user input, so its lone SetForegroundWindow is denied
/// on normal machines (background, Helper parity) — the whole point of
/// the process boundary.
/// </summary>
public sealed class SenderAction
{
    public string Kind { get; set; } = string.Empty; // "key" | "click" | "sleep"
    public int VirtualKey { get; set; }
    public double X { get; set; }
    public double Y { get; set; }
    public string Button { get; set; } = "left"; // "left" | "right"
    public int Ms { get; set; } // sleep

    public static SenderAction Key(int virtualKey) => new() { Kind = "key", VirtualKey = virtualKey };

    public static SenderAction Click(double x, double y, string button) =>
        new() { Kind = "click", X = x, Y = y, Button = button };

    public static SenderAction Sleep(int ms) => new() { Kind = "sleep", Ms = ms };
}

/// <summary>All actions for one account: one sender invocation.</summary>
public sealed class SenderWorkload
{
    public int ProcessId { get; set; }
    public bool Verbose { get; set; }
    public List<SenderAction> Actions { get; set; } = new();

    /// <summary>
    /// True when any action needs key-level activation (a real key press).
    /// Click-only batches skip the lone SetForegroundWindow entirely:
    /// clicks never needed activation (R2), so there is nothing for the
    /// lock to grant — structurally switch-free on any machine.
    /// </summary>
    public bool HasKeys =>
        Actions.Any(a => string.Equals(a.Kind, "key", StringComparison.OrdinalIgnoreCase));
}

/// <summary>Sender outcome: exit code + stdout lines mapped to counts.</summary>
public sealed class SenderResult
{
    public int Sent { get; set; }
    public int Failed { get; set; }
    public string? Error { get; set; }

    public bool Ok => Failed == 0 && Error is null;
}

/// <summary>Sender process exit codes (mirrored by PwAssistant.Sender).</summary>
public static class SenderExitCodes
{
    public const int Ok = 0;
    public const int SendError = 1;
    public const int NoWindow = 2;
    public const int BadUsage = 3;
}

/// <summary>Runs one workload in the sender process (injected seam: the
/// app spawns the real exe, tests use a fake). Never throws for transport;
/// sender-side failures surface in <see cref="SenderResult"/>.</summary>
public delegate Task<SenderResult> SenderRunner(
    SenderWorkload workload, CancellationToken cancellationToken);

/// <summary>JSON + RESULT-line codec for the sender wire (unit-tested).</summary>
public static class SenderCodec
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string ToJson(SenderWorkload workload) =>
        JsonSerializer.Serialize(workload ?? throw new ArgumentNullException(nameof(workload)), JsonOptions);

    public static SenderWorkload FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("Empty sender workload.", nameof(json));
        return JsonSerializer.Deserialize<SenderWorkload>(json, JsonOptions)
            ?? throw new ArgumentException("Null sender workload.", nameof(json));
    }

    /// <summary>
    /// Parses the sender's stdout: the trailing "RESULT sent=N failed=M"
    /// line plus an optional "ERROR ..." line. Unknown lines are ignored
    /// (diagnostics ride along verbatim). Never throws.
    /// </summary>
    public static SenderResult ParseOutput(int exitCode, IEnumerable<string> lines)
    {
        var result = new SenderResult();
        string? error = null;
        bool seenResult = false;
        foreach (string line in lines ?? Enumerable.Empty<string>())
        {
            if (line is null)
                continue;
            if (line.StartsWith("ERROR ", StringComparison.Ordinal))
                error ??= line["ERROR ".Length..].Trim();
            else if (line.StartsWith("RESULT ", StringComparison.Ordinal))
            {
                seenResult = true;
                ParseResultLine(line, result);
            }
        }
        if (!seenResult && error is null)
            error = exitCode == SenderExitCodes.Ok
                ? "missing RESULT line"
                : $"sender exit={exitCode}";
        if (exitCode == SenderExitCodes.NoWindow && error is null)
            error = "no live window for pid";
        else if (exitCode != SenderExitCodes.Ok && error is null)
            error = $"sender exit={exitCode}";
        if (error is not null)
        {
            result.Error = error;
            if (result is { Sent: 0, Failed: 0 })
                result.Failed = 1;
        }
        return result;
    }

    private static void ParseResultLine(string line, SenderResult result)
    {
        // "RESULT sent=2 failed=0" (order and extras tolerated).
        foreach (string part in line.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string[] kv = part.Split('=', 2);
            if (kv.Length != 2 || !int.TryParse(kv[1], out int value) || value < 0)
                continue;
            if (kv[0] == "sent")
                result.Sent = value;
            else if (kv[0] == "failed")
                result.Failed = value;
        }
    }
}
