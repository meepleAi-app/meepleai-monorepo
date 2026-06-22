using Api.Infrastructure.Entities.Administration;
using Microsoft.EntityFrameworkCore;

namespace Api.Infrastructure.BackgroundServices;

/// <summary>
/// Issue #885: removes orphan rows from <c>service_health_states</c> that no longer
/// correspond to any registered health check.
///
/// <para>
/// Background: <see cref="InfrastructureHealthMonitorService"/> persists health-state
/// transitions to the DB. When a health check is de-registered between deploys (e.g.
/// optional providers Unstructured/SmolDocling/Ollama deselected via configuration after
/// PR #883), the corresponding row is never updated again — it remains frozen at the
/// last persisted status. Staging investigation found rows stuck for >30 days.
/// </para>
/// <para>
/// Extracted as a static helper so the cleanup invariant is unit-testable without
/// spinning up the full <c>BackgroundService</c> host.
/// </para>
/// </summary>
internal static class HealthStateOrphanCleanup
{
    /// <summary>
    /// Removes rows from <see cref="MeepleAiDbContext.ServiceHealthStates"/> whose
    /// <c>ServiceName</c> is not present in <paramref name="registeredNames"/>.
    /// </summary>
    /// <returns>The names of removed rows (may be empty).</returns>
    public static async Task<IReadOnlyList<string>> RemoveOrphansAsync(
        MeepleAiDbContext db,
        IReadOnlySet<string> registeredNames,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(registeredNames);

        var allRows = await db.ServiceHealthStates
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var orphans = allRows
            .Where(r => !registeredNames.Contains(r.ServiceName))
            .ToList();

        if (orphans.Count == 0)
        {
            return Array.Empty<string>();
        }

        db.ServiceHealthStates.RemoveRange(orphans);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return orphans.Select(o => o.ServiceName).ToArray();
    }
}
