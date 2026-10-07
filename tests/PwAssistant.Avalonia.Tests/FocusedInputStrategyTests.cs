using PwAssistant.Avalonia.Services;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;
using PwAssistant.Core.Storage;
using PwAssistant.Core.Ux;
using PwAssistant.WinApi;

namespace PwAssistant.Avalonia.Tests;

/// <summary>
/// Regression tests for the per-fire batch scope (2026-10-07): the strategy
/// is a singleton but fires overlap (loops + manual), so mode, buffers and
/// lock ownership must belong to the fire via AsyncLocal. Before the fix,
/// one fire ending flipped another mid-flight onto the legacy in-process
/// path (fake priming + WA_INACTIVE hygiene) — the game freeze.
/// </summary>
public sealed class FocusedInputStrategyTests
{
    private sealed record TestTarget(Guid AccountId, IntPtr WindowHandle) : IWindowTarget;

    private sealed class FakeStore : IAccountStore
    {
        public Task<AppData> LoadAsync(string path, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AppData());

        public Task SaveAsync(string path, AppData data, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public void SetPassword(Account account, string plaintextPassword) =>
            account.EncryptedPassword = plaintextPassword;

        public string RevealPassword(Account account) => account.EncryptedPassword;
    }

    private sealed class FakeResolver : IWindowResolver
    {
        public IntPtr ResolveWindow(int processId) => IntPtr.Zero;
        public bool IsWindowAlive(IntPtr windowHandle) => false;
        public (int Width, int Height) GetClientSize(IntPtr windowHandle) => (0, 0);
        public (int X, int Y) ScreenToClientPoint(IntPtr windowHandle, int screenX, int screenY) => (0, 0);
        public IntPtr ResolveTopWindowAtPoint(int screenX, int screenY) => IntPtr.Zero;
        public IntPtr GetForegroundWindow() => IntPtr.Zero;
    }

    private sealed class CapturingSender
    {
        private readonly List<SenderWorkload> _workloads = new();

        public IReadOnlyList<SenderWorkload> Workloads
        {
            get { lock (_workloads) return _workloads.ToList(); }
        }

        public Task<SenderResult> RunAsync(SenderWorkload workload, CancellationToken cancellationToken)
        {
            lock (_workloads) _workloads.Add(workload);
            int sent = workload.Actions.Count(a =>
                a.Kind is "key" or "click");
            return Task.FromResult(new SenderResult { Sent = sent });
        }
    }

    private const int FakePid = 4321;
    private static readonly IntPtr BogusHwnd = new(0x1234);

    private static FocusedInputStrategy CreateStrategy(CapturingSender sender, out AppState state)
    {
        var logDir = Path.Combine(Path.GetTempPath(), "pwassistant-tests", Guid.NewGuid().ToString("N"));
        var log = new FileLogger(logDir);
        var inner = new PostMessageBackgroundStrategy();
        var store = new FakeStore();
        var localState = new AppState(store, new FakeResolver(), Path.Combine(logDir, "test.json"));
        state = localState;
        // Deterministic routing: the live OS foreground-lock timeout is
        // volatile (rewritten at runtime by launchers/helpers), so tests pin
        // worker mode instead of sampling it.
        var strategy = new FocusedInputStrategy(
            inner, localState, log, _ => FakePid, sender.RunAsync);
        strategy.TestLockTimeoutMs = uint.MaxValue;
        return strategy;
    }

    [Fact]
    public async Task OverlappingFires_DoNotLeakToLegacyPath()
    {
        var sender = new CapturingSender();
        FocusedInputStrategy strategy = CreateStrategy(sender, out _);

        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var targetA = new TestTarget(accountA, BogusHwnd);
        var targetB = new TestTarget(accountB, BogusHwnd);
        var aBegun = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var bEnded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Fire A sends again AFTER fire B fully ended. Before the per-fire
        // scope, B's EndBatch flipped the shared flag and A's second send
        // fell onto the legacy path (PostMessage to a bogus handle throws).
        Task fireA = Task.Run(async () =>
        {
            strategy.BeginBatch();
            try
            {
                await strategy.SendKeyAsync(targetA, 0x70);
                aBegun.SetResult();
                await bEnded.Task;
                await strategy.SendKeyAsync(targetA, 0x71);
                await strategy.FlushAsync(targetA);
            }
            finally
            {
                await strategy.EndBatchAsync();
            }
        });

        Task fireB = Task.Run(async () =>
        {
            await aBegun.Task;
            strategy.BeginBatch();
            try
            {
                await strategy.SendKeyAsync(targetB, 0x72);
                await strategy.FlushAsync(targetB);
            }
            finally
            {
                await strategy.EndBatchAsync();
                bEnded.SetResult();
            }
        });

        // No WinApiException: every send stayed on the worker path.
        await Task.WhenAll(fireA, fireB);

        Assert.Equal(2, sender.Workloads.Count);
        SenderWorkload workloadA = sender.Workloads.Single(w =>
            w.Actions.Any(a => a.VirtualKey == 0x70));
        SenderWorkload workloadB = sender.Workloads.Single(w =>
            w.Actions.Any(a => a.VirtualKey == 0x72));
        Assert.Equal(new[] { 0x70, 0x71 },
            workloadA.Actions.Select(a => a.VirtualKey));
        Assert.Equal(new[] { 0x72 },
            workloadB.Actions.Select(a => a.VirtualKey));
        Assert.All(sender.Workloads, w => Assert.Equal(FakePid, w.ProcessId));
    }

    [Fact]
    public async Task SendOutsideAnyBatch_GoesImmediate_NeverBuffered()
    {
        var sender = new CapturingSender();
        FocusedInputStrategy strategy = CreateStrategy(sender, out _);
        var target = new TestTarget(Guid.NewGuid(), BogusHwnd);

        // No batch scope (SyncController's live clicks): immediate legacy
        // send, which fails loudly on the bogus handle instead of buffering
        // silently into some other fire's batch.
        await Assert.ThrowsAsync<WinApiException>(() =>
            strategy.SendUiClickAsync(target, 0.5, 0.5));
        Assert.Empty(sender.Workloads);
    }

    [Fact]
    public async Task BufferedSend_StaysSilent_UntilFlush()
    {
        var sender = new CapturingSender();
        FocusedInputStrategy strategy = CreateStrategy(sender, out _);

        var target = new TestTarget(Guid.NewGuid(), BogusHwnd);
        strategy.BeginBatch();
        try
        {
            await Task.Yield(); // The scope must survive awaits (AsyncLocal).
            // Bogus handle would throw if posted: silence proves buffering.
            await strategy.SendKeyAsync(target, 0x70);
            Assert.Empty(sender.Workloads);

            await strategy.FlushAsync(target);
            SenderWorkload workload = Assert.Single(sender.Workloads);
            Assert.Equal(new[] { 0x70 },
                workload.Actions.Select(a => a.VirtualKey));
        }
        finally
        {
            await strategy.EndBatchAsync();
        }
    }

    [Fact]
    public async Task EndBatch_WithoutBegin_IsHarmlessNoOp()
    {
        var sender = new CapturingSender();
        FocusedInputStrategy strategy = CreateStrategy(sender, out _);

        // Must never throw nor corrupt the foreground-lock refcount: a
        // later real batch still acquires and sends normally.
        await strategy.EndBatchAsync();

        var target = new TestTarget(Guid.NewGuid(), BogusHwnd);
        strategy.BeginBatch();
        try
        {
            await strategy.SendKeyAsync(target, 0x70);
            await strategy.FlushAsync(target);
        }
        finally
        {
            await strategy.EndBatchAsync();
        }
        Assert.Single(sender.Workloads);
    }

    [Fact]
    public async Task LegacyAnomalyMode_SendsImmediate_NeverBuffered()
    {
        var sender = new CapturingSender();
        FocusedInputStrategy strategy = CreateStrategy(sender, out _);
        // Lock-disabled machine: every focus call granted, worker off.
        strategy.TestLockTimeoutMs = 0;

        var target = new TestTarget(Guid.NewGuid(), BogusHwnd);
        strategy.BeginBatch();
        try
        {
            // Immediate legacy send even inside a batch: fails loudly on the
            // bogus handle instead of buffering into a sender workload.
            await Assert.ThrowsAsync<WinApiException>(() =>
                strategy.SendKeyAsync(target, 0x70));
            Assert.Empty(sender.Workloads);
        }
        finally
        {
            await strategy.EndBatchAsync();
        }
    }
}
