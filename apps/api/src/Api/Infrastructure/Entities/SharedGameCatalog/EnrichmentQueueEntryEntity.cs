using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.EnrichmentQueueEntry"/> (#1874).
/// Plain POCO — all invariants enforced by the domain aggregate.
/// </summary>
public class EnrichmentQueueEntryEntity
{
    public Guid Id { get; set; }

    public Guid SharedGameId { get; set; }

    /// <summary>Persisted as int: 0=Stale, 1=Normal, 2=High.</summary>
    public EnrichmentPriority Priority { get; set; }

    public DateTimeOffset QueuedAt { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid? QueuedByUserId { get; set; }

    public bool IsProcessed { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    // === Navigation ===
    public SharedGameEntity? SharedGame { get; set; }
    public UserEntity? QueuedByUser { get; set; }
}
