using PwAssistant.Core.Ux;

namespace PwAssistant.Core.Tests;

public sealed class SingleInstanceTests
{
    [Fact]
    public async Task SecondAcquireFailsWhileFirstHeld()
    {
        // Contention from another thread: same-thread re-acquire would
        // succeed (mutexes are reentrant per owner thread), so a second
        // owner thread models the second process faithfully.
        string name = @"Local\PwAssistant.Tests.SingleInstance." + Guid.NewGuid();
        using SingleInstance? first = SingleInstance.TryAcquire(name);
        Assert.NotNull(first);

        SingleInstance? second = await Task.Run(() => SingleInstance.TryAcquire(name));
        Assert.Null(second);
    }

    [Fact]
    public void AcquireSucceedsAfterRelease()
    {
        string name = @"Local\PwAssistant.Tests.SingleInstance." + Guid.NewGuid();
        using (SingleInstance? first = SingleInstance.TryAcquire(name))
        {
            Assert.NotNull(first);
        }

        using SingleInstance? second = SingleInstance.TryAcquire(name);
        Assert.NotNull(second);
    }
}
