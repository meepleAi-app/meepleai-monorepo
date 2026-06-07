namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Priority bucket for queued BGG enrichment work (#1874).
/// Ordered High → Normal → Stale; the queue endpoint sorts DESC by this enum, ASC by QueuedAt.
/// </summary>
public enum EnrichmentPriority
{
    /// <summary>Stale skeletons batch (background sweep).</summary>
    Stale = 0,

    /// <summary>Default manual / per-game enqueue.</summary>
    Normal = 1,

    /// <summary>Forced retry, errata import, urgent admin intervention.</summary>
    High = 2,
}
