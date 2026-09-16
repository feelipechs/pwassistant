using PwHelper.Core.Execution;
using PwHelper.Core.Input;
using PwHelper.Core.Models;

namespace PwHelper.Core.Tests;

internal sealed record FakeTarget(Guid AccountId, IntPtr WindowHandle) : IWindowTarget;

internal sealed class FakeStrategy : IInputStrategy
{
    public record Call(Guid AccountId, string Kind, int? VirtualKey = null);

    public List<Call> Calls { get; } = new();
    public Func<FakeTarget, GameAction, Exception?>? FailWith { get; set; }

    public Task SendKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default)
    {
        lock (Calls) Calls.Add(new Call(target.AccountId, "key", virtualKey));
        return Task.CompletedTask;
    }

    public Task SendUiClickAsync(IWindowTarget target, double relativeX, double relativeY, CancellationToken cancellationToken = default)
    {
        lock (Calls) Calls.Add(new Call(target.AccountId, "click"));
        Exception? failure = FailWith?.Invoke((FakeTarget)target, null!);
        return failure is null ? Task.CompletedTask : Task.FromException(failure);
    }
}

public sealed class MacroExecutorTests
{
    private static readonly Guid AccountA = Guid.NewGuid();
    private static readonly Guid AccountB = Guid.NewGuid();

    private static Preset TwoAccountPreset(ExecutionMode mode) => new()
    {
        GroupId = Guid.NewGuid(),
        Name = "test",
        ExecutionMode = mode,
        Actions =
        {
            new AccountAction { AccountId = AccountA, Action = new GameAction { Type = ActionType.Key, Key = "F1" } },
            new AccountAction
            {
                AccountId = AccountB,
                Action = new GameAction
                {
                    Type = ActionType.Click,
                    RelativePosition = new RelativePosition(0.5, 0.5)
                }
            }
        }
    };

    private static Func<Guid, IWindowTarget?> Resolver(params Guid[] online) =>
        id => online.Contains(id) ? new FakeTarget(id, new IntPtr(1)) : null;

    [Fact]
    public async Task Sequential_ExecutesInOrder()
    {
        var strategy = new FakeStrategy();
        var executor = new MacroExecutor(strategy, Resolver(AccountA, AccountB));

        PresetExecutionResult result = await executor.ExecuteAsync(
            TwoAccountPreset(ExecutionMode.Sequential), Guid.NewGuid());

        Assert.Equal(2, result.Accounts.Count);
        Assert.All(result.Accounts, r => Assert.False(r.Skipped));
        Assert.Equal(AccountA, strategy.Calls[0].AccountId);
        Assert.Equal(AccountB, strategy.Calls[1].AccountId);
    }

    [Fact]
    public async Task OfflineAccount_IsSkipped_WithoutAbortingBatch()
    {
        var strategy = new FakeStrategy();
        var executor = new MacroExecutor(strategy, Resolver(AccountA));

        PresetExecutionResult result = await executor.ExecuteAsync(
            TwoAccountPreset(ExecutionMode.Sequential), Guid.NewGuid());

        Assert.True(result.Accounts[1].Skipped);
        Assert.Equal("offline", result.Accounts[1].Reason);
        Assert.False(result.Accounts[0].Skipped);
    }

    [Fact]
    public async Task Repeat_ExecutesMultipleTimes()
    {
        var strategy = new FakeStrategy();
        var executor = new MacroExecutor(strategy, Resolver(AccountA));
        var preset = new Preset
        {
            Actions =
            {
                new AccountAction
                {
                    AccountId = AccountA,
                    Action = new GameAction
                    {
                        Type = ActionType.Key,
                        Key = "F1",
                        Repeat = new RepeatSettings(Times: 3, IntervalMs: 1)
                    }
                }
            }
        };

        await executor.ExecuteAsync(preset, Guid.NewGuid());
        Assert.Equal(3, strategy.Calls.Count);
    }

    [Fact]
    public async Task Simultaneous_ExecutesAllAccounts()
    {
        var strategy = new FakeStrategy();
        var executor = new MacroExecutor(strategy, Resolver(AccountA, AccountB));

        PresetExecutionResult result = await executor.ExecuteAsync(
            TwoAccountPreset(ExecutionMode.Simultaneous), Guid.NewGuid());

        Assert.Equal(2, strategy.Calls.Count);
        Assert.All(result.Accounts, r => Assert.False(r.Skipped));
    }

    [Fact]
    public async Task StrategyError_IsCaptured_DoesNotThrow()
    {
        var strategy = new FakeStrategy
        {
            FailWith = (_, _) => new InvalidOperationException("boom")
        };
        var executor = new MacroExecutor(strategy, Resolver(AccountA, AccountB));

        PresetExecutionResult result = await executor.ExecuteAsync(
            TwoAccountPreset(ExecutionMode.Sequential), Guid.NewGuid());

        Assert.False(result.Accounts[0].Skipped);
        Assert.True(result.Accounts[1].Skipped);
        Assert.Equal("boom", result.Accounts[1].Error);
    }

    [Fact]
    public async Task Cancel_StopsBetweenActions()
    {
        var strategy = new FakeStrategy();
        var executor = new MacroExecutor(strategy, Resolver(AccountA));
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() =>
            executor.ExecuteAsync(TwoAccountPreset(ExecutionMode.Sequential), Guid.NewGuid(), cts.Token));
    }

    [Fact]
    public async Task JobRunner_CancelAll_CancelsRunningJob()
    {
        var gate = new TaskCompletionSource();
        var executor = new MacroExecutor(
            new BlockingStrategy(gate), Resolver(AccountA));
        using var runner = new MacroJobRunner(executor.ExecuteAsync);

        var preset = new Preset
        {
            Actions =
            {
                new AccountAction { AccountId = AccountA, Action = new GameAction { Type = ActionType.Key, Key = "F1", DelayBeforeMs = 30_000 } }
            }
        };

        Task<PresetExecutionResult> running = runner.ExecuteAsync(preset);
        await Task.Delay(200);
        runner.CancelAll();
        PresetExecutionResult result = await running;
        Assert.True(result.Canceled);
    }

    private sealed class BlockingStrategy : IInputStrategy
    {
        private readonly TaskCompletionSource _gate;
        public BlockingStrategy(TaskCompletionSource gate) => _gate = gate;
        public Task SendKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken);
        public Task SendUiClickAsync(IWindowTarget target, double relativeX, double relativeY, CancellationToken cancellationToken = default) =>
            Task.Delay(Timeout.Infinite, cancellationToken);
    }
}
