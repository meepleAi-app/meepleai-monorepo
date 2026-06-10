using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="WikidataCoverEnrichmentAttempt"/>.
/// Issue #1823 Wave 3 M9.
/// </summary>
internal sealed class WikidataCoverEnrichmentAttemptRepository
    : RepositoryBase, IWikidataCoverEnrichmentAttemptRepository
{
    public WikidataCoverEnrichmentAttemptRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(WikidataCoverEnrichmentAttempt attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        await DbContext.WikidataCoverEnrichmentAttempts.AddAsync(new()
        {
            Id = attempt.Id,
            SharedGameId = attempt.SharedGameId,
            AttemptedAt = attempt.AttemptedAt,
            Outcome = (int)attempt.Outcome,
            Reason = attempt.Reason,
            Details = attempt.Details,
            RetryCount = attempt.RetryCount,
            NextRetryAt = attempt.NextRetryAt,
            DeadLetteredAt = attempt.DeadLetteredAt,
        }, cancellationToken).ConfigureAwait(false);

        CollectDomainEvents(attempt);
    }

    public async Task<WikidataCoverEnrichmentAttempt?> GetLatestBySharedGameIdAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.WikidataCoverEnrichmentAttempts
            .AsNoTracking()
            .Where(a => a.SharedGameId == sharedGameId)
            .OrderByDescending(a => a.AttemptedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<Guid>> GetGameIdsDueForEnrichmentAsync(
        int limit,
        DateTime nowUtc,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            return Array.Empty<Guid>();
        }

        // Cap the per-batch limit to avoid pathological allocations even if a
        // caller passes a huge value (the scheduler is configured for 30 by
        // default).
        if (limit > 500)
        {
            limit = 500;
        }

        // Sub-query: each game's latest attempt's AttemptedAt (NULL when never attempted).
        var latestAttemptByGame =
            from a in DbContext.WikidataCoverEnrichmentAttempts.AsNoTracking()
            group a by a.SharedGameId into g
            select new { SharedGameId = g.Key, LastAt = g.Max(a => a.AttemptedAt) };

        // Join to fetch each latest-attempt row, then filter eligible games.
        var query =
            from sg in DbContext.SharedGames.AsNoTracking()
            where sg.WikidataQid != null
            join lae in latestAttemptByGame on sg.Id equals lae.SharedGameId into latestGroup
            from lae in latestGroup.DefaultIfEmpty()
            join latest in DbContext.WikidataCoverEnrichmentAttempts.AsNoTracking()
                on new { lae!.SharedGameId, AttemptedAt = lae.LastAt } equals new { latest.SharedGameId, latest.AttemptedAt }
                into latestRowGroup
            from latest in latestRowGroup.DefaultIfEmpty()
            where lae == null  // never attempted
                || (latest != null
                    && latest.Outcome == (int)WikidataCoverEnrichmentOutcome.Failed
                    && latest.NextRetryAt != null
                    && latest.NextRetryAt <= nowUtc)
            orderby sg.CreatedAt ascending
            select sg.Id;

        return await query
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    private static WikidataCoverEnrichmentAttempt Map(WikidataCoverEnrichmentAttemptEntity entity) =>
        WikidataCoverEnrichmentAttempt.Reconstitute(
            id: entity.Id,
            sharedGameId: entity.SharedGameId,
            attemptedAt: entity.AttemptedAt,
            outcome: (WikidataCoverEnrichmentOutcome)entity.Outcome,
            reason: entity.Reason,
            details: entity.Details,
            retryCount: entity.RetryCount,
            nextRetryAt: entity.NextRetryAt,
            deadLetteredAt: entity.DeadLetteredAt);
}
