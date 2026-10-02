using PwAssistant.Core.Execution;
using PwAssistant.Core.Input;
using PwAssistant.Core.Models;

namespace PwAssistant.Core.Tests;

public sealed class WorkerBatchTests
{
    [Fact]
    public void Batch_PreservesInterleavedOrder()
    {
        var batch = new WorkerBatch(4321);
        batch.AddSleep(500);
        batch.AddKey(113);
        batch.AddClick(0.5, 0.5, MouseButton.Left);
        batch.AddClick(0.1, 0.2, MouseButton.Right);
        batch.AddSleep(0); // non-positive sleeps vanish

        Assert.False(batch.IsEmpty);
        Assert.Equal(
            new[] { "sleep", "key", "click", "click" },
            batch.Actions.Select(a => a.Kind));
        Assert.Equal(500, batch.Actions[0].Ms);
        Assert.Equal(113, batch.Actions[1].VirtualKey);
        Assert.Equal("left", batch.Actions[2].Button);
        Assert.Equal("right", batch.Actions[3].Button);

        SenderWorkload workload = batch.ToWorkload(verbose: true);
        Assert.Equal(4321, workload.ProcessId);
        Assert.True(workload.Verbose);
        Assert.Equal(4, workload.Actions.Count);
    }

    [Fact]
    public void Batch_RejectsInvalidPid()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkerBatch(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkerBatch(-5));
    }

    private sealed class RecordingStrategy : IInputStrategy
    {
        public List<string> Order { get; } = new();
        public int KeysSent { get; private set; }

        public Task SendKeyAsync(IWindowTarget target, int virtualKey, CancellationToken cancellationToken = default)
        {
            KeysSent++;
            return Task.CompletedTask;
        }

        public Task SendUiClickAsync(
            IWindowTarget target, double relativeX, double relativeY,
            MouseButton button = MouseButton.Left, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task SleepAsync(IWindowTarget target, int millisecondsDelay, CancellationToken cancellationToken = default)
        {
            lock (Order) Order.Add($"sleep:{millisecondsDelay}");
            return Task.CompletedTask;
        }

        public Task FlushAsync(IWindowTarget target, CancellationToken cancellationToken = default)
        {
            lock (Order) Order.Add($"flush:{target.AccountId}");
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Executor_RoutesDelaysAndFlushThroughStrategy()
    {
        var account = Guid.NewGuid();
        var strategy = new RecordingStrategy();
        var executor = new MacroExecutor(strategy, id => new FakeTarget(id, new IntPtr(7)));
        var preset = new Preset
        {
            GroupId = Guid.NewGuid(),
            Name = "test",
            Actions =
            {
                new AccountAction
                {
                    AccountId = account,
                    Action = new GameAction
                    {
                        Type = ActionType.Key, Key = "F1", DelayBeforeMs = 250,
                        Repeat = new RepeatSettings(2, 100),
                    }
                }
            }
        };

        PresetExecutionResult result = await executor.ExecuteAsync(preset, Guid.NewGuid());

        Assert.False(result.Accounts[0].Skipped);
        // Exact order: sleep(250), key, sleep(100), key, flush.
        Assert.Equal(
            new[] { "sleep:250", "sleep:100", $"flush:{account}" },
            strategy.Order);
        Assert.Equal(2, strategy.KeysSent);
    }
}
