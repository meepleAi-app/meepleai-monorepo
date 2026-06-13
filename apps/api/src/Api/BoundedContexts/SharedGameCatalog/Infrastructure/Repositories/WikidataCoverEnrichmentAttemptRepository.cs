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
            // Task 2.3 will add: AcknowledgedAt = attempt.AcknowledgedAt
            // Task 2.3 will add: AcknowledgedBy = attempt.AcknowledgedBy
            // Task 2.3 will add: TriggeredByAdminUserId = attempt.TriggeredByAdminUserId
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

    /// <summary>
    /// Returns shared-game IDs that are ready to be enriched.
    /// </summary>
    /// <remarks>
    /// Implementation note: uses a correlated EXISTS-style sub-query over each
    /// game's latest <see cref="WikidataCoverEnrichmentAttemptEntity"/> rather
    /// than the previous double-LEFT-JOIN-with-null-forgiving pattern, because
    /// the null-forgiving operator in a join key produced a code-review HIGH
    /// finding (score 82) about EF Core potentially falling back to client
    /// evaluation under a future SDK upgrade. The MAX(AttemptedAt) sub-query
    /// is a single SARGable scan against the
    /// <c>ix_wikidata_cover_attempts_game_latest</c> composite index. Verified
    /// translates cleanly on PostgreSQL — see follow-up Testcontainers
    /// integration test in the M11/M13 admin-UI PR.
    /// </remarks>
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

        const int FailedOutcome = (int)WikidataCoverEnrichmentOutcome.Failed;

        var query =
            from sg in DbContext.SharedGames.AsNoTracking()
            where sg.WikidataQid != null
            let latestAttemptedAt = DbContext.WikidataCoverEnrichmentAttempts
                .AsNoTracking()
                .Where(a => a.SharedGameId == sg.Id)
                .Max(a => (DateTime?)a.AttemptedAt)
            let dueLatest = DbContext.WikidataCoverEnrichmentAttempts
                .AsNoTracking()
                .Where(a => a.SharedGameId == sg.Id
                    && a.AttemptedAt == latestAttemptedAt
                    && a.Outcome == FailedOutcome
                    && a.NextRetryAt != null
                    && a.NextRetryAt <= nowUtc)
                .Any()
            where latestAttemptedAt == null   // never attempted
                || dueLatest                  // latest is Failed and the backoff has elapsed
            orderby sg.CreatedAt ascending
            select sg.Id;

        return await query
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> CountDeadLettersAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.WikidataCoverEnrichmentAttempts
            .AsNoTracking()
            .CountAsync(a => a.DeadLetteredAt != null, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<int> DeleteDeadLetteredOlderThanAsync(
        DateTime cutoffUtc,
        CancellationToken cancellationToken = default)
    {
        // EF Core ExecuteDeleteAsync translates to a single DELETE FROM ...
        // WHERE ... on PostgreSQL — no SELECT round-trip, no entity tracking
        // overhead. Covered by the partial index ix_wikidata_cover_attempts_dead_letter
        // (dead_lettered_at IS NOT NULL filter) which keeps the scan small.
        return await DbContext.WikidataCoverEnrichmentAttempts
            .Where(a => a.DeadLetteredAt != null && a.DeadLetteredAt < cutoffUtc)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<DeadLetterPage> GetDeadLettersAsync(
        int skip,
        int take,
        string? reasonFilter,
        CancellationToken cancellationToken = default)
    {
        if (skip < 0) skip = 0;
        if (take < 1) take = 1;
        if (take > 200) take = 200;

        const int DeadLetterOutcome = (int)WikidataCoverEnrichmentOutcome.DeadLetter;

        var query =
            from a in DbContext.WikidataCoverEnrichmentAttempts.AsNoTracking()
            where a.DeadLetteredAt != null && a.Outcome == DeadLetterOutcome
            select a;

        if (!string.IsNullOrWhiteSpace(reasonFilter))
        {
            query = query.Where(a => a.Reason == reasonFilter);
        }

        var totalCount = await query
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        if (totalCount == 0)
        {
            return new DeadLetterPage(Array.Empty<DeadLetterRow>(), 0);
        }

        // Join to shared_games for the denormalized title.
        // IgnoreQueryFilters() defeats the SharedGameEntity soft-delete query
        // filter — without this, INNER JOIN would silently drop dead-letter
        // rows whose parent game has been soft-deleted within the 7-day
        // retention window, producing a count/items mismatch that breaks the
        // FE paginator (totalCount > items.Count). LEFT-join idiom with a
        // fallback string also defends against an orphaned FK on a
        // hard-deleted game (cascade should prevent it, but defensive).
        var pageQuery =
            from a in query.OrderByDescending(x => x.DeadLetteredAt).Skip(skip).Take(take)
            join sg in DbContext.SharedGames.AsNoTracking().IgnoreQueryFilters()
                on a.SharedGameId equals sg.Id into sgs
            from sg in sgs.DefaultIfEmpty()
            select new DeadLetterRow(
                a.Id,
                a.SharedGameId,
                sg != null ? sg.Title : "(deleted game)",
                a.AttemptedAt,
                a.DeadLetteredAt!.Value,
                a.Reason,
                a.Details,
                a.RetryCount);

        var items = await pageQuery
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new DeadLetterPage(items, totalCount);
    }

    public async Task<IReadOnlyDictionary<Guid, Guid>> GetSharedGameIdsByAttemptIdsAsync(
        IReadOnlyCollection<Guid> attemptIds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attemptIds);

        if (attemptIds.Count == 0)
        {
            return new Dictionary<Guid, Guid>();
        }

        // Defensive cap mirroring the F2 validator MaxBatchSize (50). EF Core
        // translates a Contains() against a parameterized list into a single
        // SARGable WHERE id = ANY(@p) on PostgreSQL.
        var ids = attemptIds.Distinct().Take(50).ToArray();

        var rows = await DbContext.WikidataCoverEnrichmentAttempts
            .AsNoTracking()
            .Where(a => ids.Contains(a.Id))
            .Select(a => new { a.Id, a.SharedGameId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.ToDictionary(r => r.Id, r => r.SharedGameId);
    }

    public async Task<IReadOnlyList<WikidataCoverEnrichmentAttempt>> GetAttemptsByGameIdAsync(
        Guid sharedGameId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        limit = Math.Clamp(limit, 1, 200);

        var entities = await DbContext.WikidataCoverEnrichmentAttempts
            .AsNoTracking()
            .Where(a => a.SharedGameId == sharedGameId)
            .OrderByDescending(a => a.AttemptedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(Map).ToList();
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
            deadLetteredAt: entity.DeadLetteredAt,
            acknowledgedAt: null,            // Task 2.3 will replace with entity.AcknowledgedAt
            acknowledgedBy: null,            // Task 2.3 will replace with entity.AcknowledgedBy
            triggeredByAdminUserId: null);   // Task 2.3 will replace with entity.TriggeredByAdminUserId
}
