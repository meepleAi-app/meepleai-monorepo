namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.EnrichmentAttempt"/> (#1874).
/// Plain POCO — all invariants enforced by the domain aggregate.
/// </summary>
public class EnrichmentAttemptEntity
{
    public Guid Id { get; set; }

    public Guid SharedGameId { get; set; }

    public Guid? CatalogSyncRunId { get; set; }

    public DateTimeOffset AttemptedAt { get; set; }

    public bool Success { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorDetail { get; set; }

    public int RetryCount { get; set; }

    // === Navigation ===
    public SharedGameEntity? SharedGame { get; set; }
    public CatalogSyncRunEntity? CatalogSyncRun { get; set; }
}
