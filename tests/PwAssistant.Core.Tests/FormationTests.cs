using PwAssistant.Core.Models;

namespace PwAssistant.Core.Tests;

public sealed class FormationApplicatorTests
{
    [Fact]
    public void Apply_PreservesOrder_AndSkipsUnknown()
    {
        Guid a = Guid.NewGuid(), b = Guid.NewGuid(), ghost = Guid.NewGuid();
        var formation = new Formation { Name = "PT", AccountIds = [a, ghost, b] };

        (IReadOnlyList<Guid> applied, int skipped) =
            FormationApplicator.Apply(formation, [b, a]);

        Assert.Equal([a, b], applied);
        Assert.Equal(1, skipped);
    }

    [Fact]
    public void Apply_EmptyFormation_AppliesNothing()
    {
        (IReadOnlyList<Guid> applied, int skipped) =
            FormationApplicator.Apply(new Formation(), [Guid.NewGuid()]);

        Assert.Empty(applied);
        Assert.Equal(0, skipped);
    }

    [Fact]
    public void Apply_RejectsNulls()
    {
        Assert.Throws<ArgumentNullException>(() =>
            FormationApplicator.Apply(null!, [Guid.NewGuid()]));
        Assert.Throws<ArgumentNullException>(() =>
            FormationApplicator.Apply(new Formation(), null!));
    }
}
