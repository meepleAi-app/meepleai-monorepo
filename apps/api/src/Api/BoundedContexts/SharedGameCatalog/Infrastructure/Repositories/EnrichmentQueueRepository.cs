using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// EF Core repository for <see cref="EnrichmentQueueEntry"/> (#1874).
/// </summary>
internal sealed class EnrichmentQueueRepository : RepositoryBase, IEnrichmentQueueRepository
{
    public EnrichmentQueueRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(EnrichmentQueueEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        await DbContext.EnrichmentQueueEntries
            .AddAsync(MapToEntity(entry), cancellationToken)
            .ConfigureAwait(false);

        CollectDomainEvents(entry);
    }

    public async Task AddRangeAsync(IEnumerable<EnrichmentQueueEntry> entries, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var list = entries.ToList();
        var mapped = list.Select(MapToEntity).ToList();

        await DbContext.EnrichmentQueueEntries
            .AddRangeAsync(mapped, cancellationToken)
            .ConfigureAwait(false);

        foreach (var entry in list)
        {
            CollectDomainEvents(entry);
        }
    }

    public async Task<(IReadOnlyList<EnrichmentQueueEntryWithTitle> Items, int Total)> GetPendingAsync(
        EnrichmentPriority? priority,
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit < 1 || limit > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be 1-100.");
        }

        // Join queue entries with shared_games to fetch title in one pass.
        // The HasQueryFilter on SharedGameEntity already excludes soft-deleted rows,
        // so the inner-join semantics drop entries whose game has been removed.
        var baseQuery =
            from entry in DbContext.EnrichmentQueueEntries.AsNoTracking()
            join game in DbContext.SharedGames.AsNoTracking() on entry.SharedGameId equals game.Id
            where !entry.IsProcessed
            select new { entry, game.Title };

        if (priority.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.entry.Priority == priority.Value);
        }

        var total = await baseQuery.CountAsync(cancellationToken).ConfigureAwait(false);

        var rows = await baseQuery
            .OrderByDescending(x => x.entry.Priority)
            .ThenBy(x => x.entry.QueuedAt)
            .Take(limit)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows
            .Select(r => new EnrichmentQueueEntryWithTitle(MapToDomain(r.entry), r.Title))
            .ToList();

        return (items, total);
    }

    public async Task<IReadOnlyList<EnrichmentQueueEntry>> GetPendingForGameAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        if (sharedGameId == Guid.Empty)
        {
            throw new ArgumentException("SharedGameId cannot be Guid.Empty.", nameof(sharedGameId));
        }

        var rows = await DbContext.EnrichmentQueueEntries
            .AsNoTracking()
            .Where(e => e.SharedGameId == sharedGameId && !e.IsProcessed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(MapToDomain).ToList();
    }

    public Task UpdateAsync(EnrichmentQueueEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var entity = MapToEntity(entry);
        DbContext.EnrichmentQueueEntries.Update(entity);

        CollectDomainEvents(entry);
        return Task.CompletedTask;
    }

    // === Mapping ===

    private static EnrichmentQueueEntry MapToDomain(EnrichmentQueueEntryEntity e) =>
        EnrichmentQueueEntry.Reconstitute(
            id: e.Id,
            sharedGameId: e.SharedGameId,
            priority: e.Priority,
            reason: e.Reason,
            queuedByUserId: e.QueuedByUserId,
            queuedAt: e.QueuedAt,
            isProcessed: e.IsProcessed,
            processedAt: e.ProcessedAt);

    private static EnrichmentQueueEntryEntity MapToEntity(EnrichmentQueueEntry e) => new()
    {
        Id = e.Id,
        SharedGameId = e.SharedGameId,
        Priority = e.Priority,
        Reason = e.Reason,
        QueuedByUserId = e.QueuedByUserId,
        QueuedAt = e.QueuedAt,
        IsProcessed = e.IsProcessed,
        ProcessedAt = e.ProcessedAt,
    };
}
