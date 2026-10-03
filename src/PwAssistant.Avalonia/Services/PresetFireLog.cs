using PwAssistant.Core.Execution;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.Avalonia.Services;

/// <summary>
/// Single fire-log format for every trigger path (manual, hotkey, loop).
/// One summary line plus one line per account with the live window state
/// at log time: pid, hwnd, alive/visible/minimized/foreground, outcome.
/// Secrets never appear here (names only, same as StatusMessage).
/// </summary>
public static class PresetFireLog
{
    public static void Log(
        FileLogger log, IWindowResolver resolver, Preset preset,
        PresetExecutionResult result, string origin,
        Func<Guid, (string Name, int? Pid)> accountInfo, bool verbose = false)
    {
        string job = result.JobId.ToString("N")[..8];
        if (result.Canceled)
        {
            log.Info($"Fired preset {preset.Name} ({origin}, job={job}): canceled.");
            return;
        }

        int fired = result.Accounts.Count(r => !r.Skipped);
        int skipped = result.Accounts.Count(r => r.Skipped);
        log.Info($"Fired preset {preset.Name} ({origin}, job={job}, {preset.Actions.Count} actions): fired={fired} skipped={skipped}.");

        IntPtr foreground;
        try
        {
            foreground = resolver.GetForegroundWindow();
        }
        catch
        {
            foreground = IntPtr.Zero;
        }

        foreach (AccountExecutionResult account in result.Accounts)
        {
            (string name, int? pid) = accountInfo(account.AccountId);
            IntPtr hwnd = account.WindowHandle;
            string outcome = account.Skipped
                ? $"skip({account.Reason ?? account.Error ?? "?"})"
                : "sent";
            log.Info($"  [job={job}] {name} pid={pid?.ToString() ?? "-"} hwnd=0x{hwnd:X}"
                + $" alive={(WindowFocus.IsAlive(hwnd) ? 1 : 0)}"
                + $" vis={(WindowFocus.IsVisible(hwnd) ? 1 : 0)}"
                + $" min={(WindowFocus.IsMinimized(hwnd) ? 1 : 0)}"
                + $" fg={(hwnd != IntPtr.Zero && hwnd == foreground ? 1 : 0)}"
                + $" {outcome}");
            if (verbose && !account.Skipped && pid.HasValue)
            {
                IReadOnlyList<string> matches = WindowDiagnostics.DescribeWindows(pid.Value);
                log.Info($"  [job={job}] {name} matches({matches.Count}): {string.Join("; ", matches)}");
            }
        }

        if (skipped > 0)
        {
            string reasons = string.Join("; ", result.Accounts
                .Where(r => r.Skipped)
                .GroupBy(r => r.Reason ?? r.Error ?? "?")
                .Select(g => $"{g.Key}x{g.Count()}"));
            log.Warn($"Preset {preset.Name} skipped: {reasons}.");
        }
    }
}
