using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for <see cref="EnrichmentQueueEntry"/> (#1874).
/// </summary>
public interface IEnrichmentQueueRepository
{
    /// <summary>Stages a new queue entry for insertion. Persistence at SaveChangesAsync.</summary>
    Task AddAsync(EnrichmentQueueEntry entry, CancellationToken cancellationToken = default);

    /// <summary>Stages a batch insert (used by enqueue-all-skeletons batch).</summary>
    Task AddRangeAsync(IEnumerable<EnrichmentQueueEntry> entries, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns pending (IsProcessed=false) entries joined with their shared-game title,
    /// ordered by Priority DESC + QueuedAt ASC. Filters by <paramref name="priority"/> when supplied,
    /// limits to <paramref name="limit"/> rows (caller caps at 100), and skips entries whose shared
    /// game has been soft-deleted.
    /// </summary>
    Task<(IReadOnlyList<EnrichmentQueueEntryWithTitle> Items, int Total)> GetPendingAsync(
        EnrichmentPriority? priority,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all pending (IsProcessed=false) entries for a specific shared game.
    /// Used by <see cref="Api.Infrastructure.BackgroundServices.BggImportQueueBackgroundService"/>
    /// (#1907) to cascade MarkProcessed when a BGG enrichment iteration reaches a terminal
    /// outcome (success or max-retry failure).
    /// </summary>
    /// <remarks>
    /// Multiple entries can exist for the same game (e.g. an admin manually enqueues a
    /// Normal-priority entry while a Stale-priority entry from the skeleton sweep is
    /// already pending). All of them collapse to Processed on the same terminal outcome.
    /// </remarks>
    Task<IReadOnlyList<EnrichmentQueueEntry>> GetPendingForGameAsync(
        Guid sharedGameId,
        CancellationToken cancellationToken = default);

    /// <summary>Persists changes to a previously loaded entry (e.g. MarkProcessed).</summary>
    Task UpdateAsync(EnrichmentQueueEntry entry, CancellationToken cancellationToken = default);
}

/// <summary>
/// Read-model projection joining <see cref="EnrichmentQueueEntry"/> with the shared-game title
/// to avoid an N+1 round-trip from the query handler.
/// </summary>
public sealed record EnrichmentQueueEntryWithTitle(
    EnrichmentQueueEntry Entry,
    string SharedGameTitle);
