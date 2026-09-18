using PwAssistant.Core.Models;

namespace PwAssistant.Core.Sync;

/// <summary>
/// Sync Click core: converts a physical click on the master window into
/// client-area fractions, then back into replica pixels per window.
/// Fractions tolerate windows of different sizes. Only validated UI clicks
/// replicate — 3D-ground clicks are out of v1 scope and rejected here.
/// Replicas are posted messages (invisible to the mouse hook), so there is
/// no echo loop by construction.
/// </summary>
public sealed class SyncService
{
    private readonly HashSet<Guid> _syncedAccounts = new();
    private readonly object _gate = new();

    public bool Enabled { get; private set; }

    public void SetEnabled(bool enabled) => Enabled = enabled;

    public void SetSyncedAccounts(IEnumerable<Guid> accountIds)
    {
        lock (_gate)
        {
            _syncedAccounts.Clear();
            foreach (Guid id in accountIds)
                _syncedAccounts.Add(id);
        }
    }

    public bool IsSynced(Guid accountId)
    {
        lock (_gate)
            return _syncedAccounts.Contains(accountId);
    }

    /// <summary>
    /// Master pixel click → fraction. Returns null when the click must NOT
    /// replicate (service off, non-left button, outside client area).
    /// </summary>
    public RelativePosition? CaptureMasterClick(
        int clickX, int clickY, int clientWidth, int clientHeight, bool isLeftButton)
    {
        if (!Enabled || !isLeftButton)
            return null;
        if (clientWidth <= 0 || clientHeight <= 0)
            return null;
        if (clickX < 0 || clickY < 0 || clickX > clientWidth || clickY > clientHeight)
            return null;

        return RelativePosition.FromAbsolute(clickX, clickY, clientWidth, clientHeight);
    }

    /// <summary>Fraction → replica window pixels.</summary>
    public static (int X, int Y) ToReplicaPixels(
        RelativePosition fraction, int clientWidth, int clientHeight) =>
        fraction.ToAbsolute(clientWidth, clientHeight);
}
