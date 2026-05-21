using Api.BoundedContexts.SessionTracking.Domain.Entities;

namespace Api.BoundedContexts.SessionTracking.Domain.Repositories;

/// <summary>
/// Persistence boundary for <see cref="SessionBookProgress"/> (Task C1).
/// One row per (campaign, book) pair; lookups are typically scoped by
/// campaign session id, with optional drill-down by book id.
/// </summary>
public interface ISessionBookProgressRepository
{
    Task<SessionBookProgress?> GetByCampaignAndBookAsync(Guid campaignSessionId, Guid gameBookId, CancellationToken cancellationToken);
    Task<IReadOnlyList<SessionBookProgress>> ListByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the most recently visited <see cref="SessionBookProgress"/> for a campaign,
    /// or <c>null</c> when the campaign has no progress rows yet. "Most recent" = highest
    /// <see cref="SessionBookProgress.LastVisitedAt"/>. Used by Get/List campaign queries
    /// to populate the legacy <c>CurrentParagraph</c>/<c>History</c> DTO fields with the
    /// last book the user was reading.
    /// </summary>
    Task<SessionBookProgress?> GetMostRecentByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken);

    Task AddAsync(SessionBookProgress progress, CancellationToken cancellationToken);
    Task UpdateAsync(SessionBookProgress progress, CancellationToken cancellationToken);

    /// <summary>
    /// Issue #1394: stages a delete for every <see cref="SessionBookProgress"/> row tied to the
    /// given campaign session, eliminating orphans left behind by campaign soft-delete (the
    /// table has no FK cascade — app-layer integrity, parity with sibling SessionTracking
    /// entities). The actual flush happens via the campaign repo's <c>SaveChangesAsync</c>
    /// (unit-of-work pattern), so callers must invoke it AFTER staging the delete to keep
    /// soft-delete + orphan removal atomic.
    /// </summary>
    Task DeleteByCampaignAsync(Guid campaignSessionId, CancellationToken cancellationToken);
}
