using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="EnrichmentAttempt"/> (#1874).
/// </summary>
internal sealed class EnrichmentAttemptRepository : RepositoryBase, IEnrichmentAttemptRepository
{
    public EnrichmentAttemptRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(EnrichmentAttempt attempt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(attempt);

        await DbContext.EnrichmentAttempts.AddAsync(new()
        {
            Id = attempt.Id,
            SharedGameId = attempt.SharedGameId,
            CatalogSyncRunId = attempt.CatalogSyncRunId,
            AttemptedAt = attempt.AttemptedAt,
            Success = attempt.Success,
            ErrorCode = attempt.ErrorCode,
            ErrorDetail = attempt.ErrorDetail,
            RetryCount = attempt.RetryCount,
        }, cancellationToken).ConfigureAwait(false);

        CollectDomainEvents(attempt);
    }

    public async Task<(IReadOnlyList<FailedItemAggregate> Items, int Total)> GetFailedAggregatesAsync(
        int days,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (days < 1 || days > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(days), days, "Days must be 1-365.");
        }

        if (limit < 1 || limit > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be 1-100.");
        }

        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);

        // Stage 1 (DB): for each shared game with at least one failed attempt in the window,
        // find the MAX(attempted_at). EF translates this GroupBy + Max cleanly.
        var maxPerGame = await (
            from attempt in DbContext.EnrichmentAttempts.AsNoTracking()
            where !attempt.Success && attempt.AttemptedAt >= cutoff
            join game in DbContext.SharedGames.AsNoTracking() on attempt.SharedGameId equals game.Id
            group attempt by attempt.SharedGameId into g
            select new { SharedGameId = g.Key, LastAt = g.Max(a => a.AttemptedAt) })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var total = maxPerGame.Count;

        if (total == 0)
        {
            return (Array.Empty<FailedItemAggregate>(), 0);
        }

        // Stage 2 (DB): fetch the corresponding attempt + game-title rows for those pairs.
        // Translated into a single IN over composite key tuples on PG.
        var topGameIds = maxPerGame
            .OrderByDescending(x => x.LastAt)
            .Take(limit)
            .Select(x => x.SharedGameId)
            .ToList();

        var topTimestamps = maxPerGame
            .Where(x => topGameIds.Contains(x.SharedGameId))
            .ToDictionary(x => x.SharedGameId, x => x.LastAt);

        var rows = await (
            from attempt in DbContext.EnrichmentAttempts.AsNoTracking()
            join game in DbContext.SharedGames.AsNoTracking() on attempt.SharedGameId equals game.Id
            where topGameIds.Contains(attempt.SharedGameId) && !attempt.Success
            select new
            {
                attempt.SharedGameId,
                game.Title,
                attempt.ErrorCode,
                attempt.ErrorDetail,
                attempt.AttemptedAt,
                attempt.RetryCount,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Stage 3 (memory): keep only the row matching the per-game MAX timestamp.
        var items = topGameIds
            .Select(gid =>
            {
                var lastAt = topTimestamps[gid];
                var row = rows.First(r => r.SharedGameId == gid && r.AttemptedAt == lastAt);
                return new FailedItemAggregate(
                    SharedGameId: row.SharedGameId,
                    SharedGameTitle: row.Title,
                    ErrorCode: row.ErrorCode ?? string.Empty,
                    ErrorDetail: row.ErrorDetail ?? string.Empty,
                    LastAttemptAt: row.AttemptedAt,
                    RetryCount: row.RetryCount);
            })
            .ToList();

        return (items, total);
    }
}
