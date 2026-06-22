namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Aggregates.CatalogSyncRun"/> (#1861).
/// Integer codes are stored as-is in the <c>status</c> column of <c>catalog_sync_runs</c>.
/// </summary>
/// <remarks>
/// State machine (transitions enforced by the aggregate):
/// <code>
///   Queued (0)  ──MarkRunning────► Running (1)
///   Running (1) ──Complete───────► Success  (2)
///   Running (1) ──Fail───────────► Failed   (3)
///   Running (1) ──TimeOut────────► TimedOut (4)
///   Queued (0)  ──Fail───────────► Failed   (3)   (pipeline error before pickup)
/// </code>
/// Success / Failed / TimedOut are terminal — no further transitions.
/// </remarks>
public enum CatalogSyncStatus
{
    Queued = 0,
    Running = 1,
    Success = 2,
    Failed = 3,
    TimedOut = 4
}
