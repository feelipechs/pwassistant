namespace PwAssistant.Core.Models;

/// <summary>
/// Pure formation application: intersects a formation's ordered member ids
/// with the accounts known to the store. Unknown ids are reported as
/// skipped — the caller applies the rest and never aborts.
/// </summary>
public static class FormationApplicator
{
    public static (IReadOnlyList<Guid> Applied, int Skipped) Apply(
        Formation formation, IEnumerable<Guid> knownAccountIds)
    {
        ArgumentNullException.ThrowIfNull(formation);
        ArgumentNullException.ThrowIfNull(knownAccountIds);

        var known = new HashSet<Guid>(knownAccountIds);
        var applied = new List<Guid>();
        int skipped = 0;
        foreach (Guid id in formation.AccountIds)
        {
            if (known.Contains(id))
                applied.Add(id);
            else
                skipped++;
        }
        return (applied, skipped);
    }
}
