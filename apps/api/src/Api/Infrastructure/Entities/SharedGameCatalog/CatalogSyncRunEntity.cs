using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

namespace Api.Infrastructure.Entities.SharedGameCatalog;

/// <summary>
/// Persistence entity for <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.CatalogSyncRun"/>
/// (#1861, F4-A6 BE). Plain POCO — all invariants enforced by the domain aggregate.
/// </summary>
public class CatalogSyncRunEntity
{
    public Guid Id { get; set; }

    /// <summary>Persisted as int: 0=BggApi, 1=CsvImport, 2=Manual.</summary>
    public CatalogSyncProvider Provider { get; set; }

    /// <summary>Persisted as int: 0=Queued, 1=Running, 2=Success, 3=Failed, 4=TimedOut.</summary>
    public CatalogSyncStatus Status { get; set; }

    public string Title { get; set; } = string.Empty;

    public Guid? TriggeredByUserId { get; set; }

    public int ItemsAdded { get; set; }
    public int ItemsUpdated { get; set; }
    public int ItemsFailed { get; set; }

    public string? ErrorCode { get; set; }
    public string? ErrorDetail { get; set; }
    public string? LogTailJsonPath { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }

    // === Navigation (optional, FK can be null for cron-triggered runs) ===
    public UserEntity? TriggeredByUser { get; set; }
}
