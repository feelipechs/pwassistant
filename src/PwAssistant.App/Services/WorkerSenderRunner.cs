using System.Diagnostics;
using System.IO;
using System.Text;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Ux;

namespace PwAssistant.App.Services;

/// <summary>
/// Spawns the separate sender process (PwAssistant.Sender.exe) — one
/// invocation per account per fire. The sender never receives user input
/// (no window, no hooks, spawned and reaped by us), so its lone
/// SetForegroundWindow is denied on normal machines: background, Helper
/// parity. Transport never throws (except cancellation, which kills the
/// child and rethrows); sender-side failures surface in
/// <see cref="SenderResult"/> and become per-account skip-with-error.
/// </summary>
public static class WorkerSenderRunner
{
    public const string ExeFileName = "PwAssistant.Sender.exe";

    /// <summary>
    /// Sender next to the app (copied there by dev builds, published
    /// self-contained single-file for release). Only the exe is required:
    /// single-file publish has no managed dll next to it (bundled inside),
    /// so demanding one breaks every installed copy. Launch failures
    /// surface through stderr instead (see Create).
    /// </summary>
    public static string LocateExe()
    {
        string dir = AppContext.BaseDirectory;
        string exe = Path.Combine(dir, ExeFileName);
        if (!File.Exists(exe))
            throw new FileNotFoundException(
                $"Sender worker not found at {exe}. Build the solution so it is copied next to the app.", exe);
        return exe;
    }

    public static int? ResolvePid(AppState state, Guid accountId)
    {
        ArgumentNullException.ThrowIfNull(state);
        foreach (Server server in state.Data.Servers)
            foreach (Account account in server.Accounts)
                if (account.Id == accountId)
                    return account.ProcessId;
        return null;
    }

    public static SenderRunner Create(FileLogger log, AppState state)
    {
        ArgumentNullException.ThrowIfNull(log);
        ArgumentNullException.ThrowIfNull(state);
        return async (workload, cancellationToken) =>
        {
            string exe;
            try
            {
                exe = LocateExe();
            }
            catch (Exception ex)
            {
                return new SenderResult { Error = ex.Message, Failed = 1 };
            }

            var psi = new ProcessStartInfo(exe)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            if (workload.Verbose)
                psi.ArgumentList.Add("--verbose");

            using var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
            var lines = new List<string>();
            var stderr = new StringBuilder();
            proc.OutputDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    lock (lines) lines.Add(e.Data);
            };
            proc.ErrorDataReceived += (_, e) =>
            {
                if (e.Data is not null)
                    lock (stderr) stderr.AppendLine(e.Data);
            };

            try
            {
                if (!proc.Start())
                    return new SenderResult { Error = "sender did not start", Failed = 1 };
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                // Small JSON, non-blocking write; cancellation is observed
                // at WaitForExitAsync below (which kills on cancel).
                await proc.StandardInput.WriteAsync(SenderCodec.ToJson(workload)).ConfigureAwait(false);
                await proc.StandardInput.FlushAsync().ConfigureAwait(false);
                proc.StandardInput.Close();
                await proc.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
                proc.WaitForExit(); // drain async output handlers (documented pattern)
            }
            catch (OperationCanceledException)
            {
                try
                {
                    if (!proc.HasExited)
                        proc.Kill();
                }
                catch
                {
                    // Kill is best effort; the cancellation itself propagates.
                }
                throw;
            }
            catch (Exception ex)
            {
                return new SenderResult { Error = ex.Message, Failed = 1 };
            }

            List<string> output;
            lock (lines) output = new List<string>(lines);
            string errText;
            lock (stderr) errText = stderr.ToString().Trim();
            SenderResult result = SenderCodec.ParseOutput(proc.ExitCode, output);
            if (!result.Ok && errText.Length > 0)
            {
                // The host writes the real cause here (e.g. missing dll or
                // runtime: "The application to execute does not exist...").
                // Without this, failures are just an exit code.
                const int maxStderr = 300;
                string tail = errText.Length > maxStderr
                    ? string.Concat("…", errText.AsSpan(errText.Length - maxStderr))
                    : errText;
                result.Error = string.IsNullOrEmpty(result.Error)
                    ? tail
                    : string.Concat(result.Error, " | ", tail);
            }

            bool verbose = workload.Verbose || state.VerboseFireLog;
            if (verbose)
                foreach (string line in output)
                    log.Info(line);
            else if (!result.Ok)
                foreach (string line in output)
                    log.Warn(line);
            return result;
        };
    }
}
