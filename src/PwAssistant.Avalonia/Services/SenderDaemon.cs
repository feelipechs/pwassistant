using System.Diagnostics;
using System.Text;
using PwAssistant.Core.Input;
using PwAssistant.Core.Ux;

namespace PwAssistant.Avalonia.Services;

/// <summary>
/// One parked sender process per app session (Helper parity). Flushes go
/// over the open stdin as newline-delimited workloads; RESULT lines come
/// back on stdout. At-most-once per flush: a daemon death mid-flush fails
/// that flush honestly (never retried — repeating a toggle could dismount),
/// the next flush respawns transparently. Cancel kills the daemon and
/// rethrows. When the daemon cannot start at all, falls back to one-shot
/// spawn (today's behavior) with a warning. Thread-safe: overlapping fires
/// serialize on a semaphore (same order as today, just faster).
/// </summary>
public sealed class SenderDaemon : IDisposable
{
    private readonly FileLogger _log;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _io = new(1, 1);
    private Process? _proc;
    private List<string>? _lines;
    private TaskCompletionSource<bool>? _gotResult;
    private bool _disposed;

    public SenderDaemon(FileLogger log)
    {
        _log = log ?? throw new ArgumentNullException(nameof(log));
    }

    public async Task<SenderResult> RunAsync(SenderWorkload workload, bool verbose, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(workload);
        await _io.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Process? proc;
            lock (_gate)
            {
                ObjectDisposedException.ThrowIf(_disposed, this);
                proc = _proc;
                if (proc is not null && HasExitedQuietly(proc))
                {
                    DiscardLocked();
                    proc = null;
                }
            }
            if (proc is null)
            {
                proc = StartLocked();
                if (proc is null)
                {
                    _log.Warn("Sender daemon unavailable; one-shot fallback for this flush.");
                    return await WorkerSenderRunner.RunOneShotAsync(
                        workload, verbose, _log, cancellationToken).ConfigureAwait(false);
                }
            }

            var lines = new List<string>();
            var gotResult = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_gate)
            {
                _lines = lines;
                _gotResult = gotResult;
            }
            try
            {
                await proc.StandardInput.WriteLineAsync(SenderCodec.ToJson(workload)).ConfigureAwait(false);
                await proc.StandardInput.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Pipe dead (daemon died silently): fail honestly, drop it so
                // the next flush respawns. Never retry: delivery unknown.
                KillLocked();
                return Fail($"sender write failed: {ex.Message}");
            }

            bool completed;
            using (cancellationToken.Register(() => gotResult.TrySetCanceled()))
            {
                try
                {
                    completed = await gotResult.Task.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    KillLocked();
                    throw;
                }
            }
            if (!completed)
            {
                // Process exited mid-flush without RESULT: fail honestly.
                int exit = 1;
                try
                {
                    if (proc.HasExited)
                        exit = proc.ExitCode;
                }
                catch
                {
                    // Best effort; exit stays 1.
                }
                KillLocked();
                List<string> output;
                lock (_gate) output = new List<string>(lines);
                SenderResult dead = SenderCodec.ParseOutput(exit, output);
                WorkerSenderRunner.LogOutput(_log, verbose, output, dead);
                if (dead.Error is null)
                    dead.Error = "sender died mid-flush";
                if (dead is { Sent: 0, Failed: 0 })
                    dead.Failed = 1;
                return dead;
            }

            List<string> done;
            lock (_gate)
            {
                done = new List<string>(lines);
                _lines = null;
                _gotResult = null;
            }
            SenderResult result = SenderCodec.ParseOutput(0, done);
            WorkerSenderRunner.LogOutput(_log, verbose, done, result);
            return result;
        }
        finally
        {
            _io.Release();
        }
    }

    /// <summary>Spawns the parked daemon (or null when impossible).</summary>
    private Process? StartLocked()
    {
        string exe;
        try
        {
            exe = WorkerSenderRunner.LocateExe();
        }
        catch (Exception ex)
        {
            _log.Warn($"Sender daemon cannot start: {ex.Message}");
            return null;
        }

        var psi = new ProcessStartInfo(exe, "--daemon")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };
        proc.OutputDataReceived += (_, e) =>
        {
            if (e.Data is null)
                return;
            lock (_gate)
            {
                _lines?.Add(e.Data);
                if (e.Data.StartsWith("RESULT ", StringComparison.Ordinal))
                    _gotResult?.TrySetResult(true);
            }
        };
        proc.Exited += (_, _) =>
        {
            lock (_gate)
            {
                _gotResult?.TrySetResult(false);
            }
        };
        try
        {
            if (!proc.Start())
            {
                proc.Dispose();
                return null;
            }
        }
        catch
        {
            proc.Dispose();
            return null;
        }
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();
        lock (_gate)
        {
            _proc = proc;
        }
        return proc;
    }

    private static SenderResult Fail(string message) =>
        new() { Error = message, Failed = 1 };

    private static bool HasExitedQuietly(Process proc)
    {
        try
        {
            return proc.HasExited;
        }
        catch
        {
            return true;
        }
    }

    /// <summary>Forgets the child (caller holds no lock assumptions).</summary>
    private void DiscardLocked()
    {
        try
        {
            _proc?.Dispose();
        }
        catch
        {
            // Best effort by contract.
        }
        _proc = null;
        _lines = null;
        _gotResult = null;
    }

    private void KillLocked()
    {
        try
        {
            if (_proc is not null && !HasExitedQuietly(_proc))
                _proc.Kill();
        }
        catch
        {
            // Kill is best effort; the cancellation itself propagates.
        }
        DiscardLocked();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            _disposed = true;
        }
        KillLocked();
        _io.Dispose();
    }
}
