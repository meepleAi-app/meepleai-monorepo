using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for the <see cref="CatalogSyncRun"/> aggregate (#1861, F4-A6 BE).
/// </summary>
public interface ICatalogSyncRunRepository
{
    /// <summary>Stages a new run for insertion. Persistence at SaveChangesAsync.</summary>
    Task AddAsync(CatalogSyncRun run, CancellationToken cancellationToken = default);

    /// <summary>Loads a run by primary key. Returns <c>null</c> when not found.</summary>
    Task<CatalogSyncRun?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the run currently in <see cref="Enums.CatalogSyncStatus.Running"/>, if any.
    /// Used by TriggerCatalogSyncCommand to enforce single-running invariant (409 conflict).
    /// </summary>
    Task<CatalogSyncRun?> GetCurrentRunningAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent terminal (Success / Failed / TimedOut) run by <c>CreatedAt</c> DESC.
    /// Used by GetCatalogSyncStatusQuery to populate "lastRun" in idle status.
    /// </summary>
    Task<CatalogSyncRun?> GetLatestCompletedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Paged history ordered by <c>CreatedAt</c> DESC. <paramref name="page"/> 1-based,
    /// <paramref name="pageSize"/> capped at 100 by handler. Returns items + total count.
    /// </summary>
    Task<(IReadOnlyList<CatalogSyncRun> Items, int Total)> GetPagedAsync(
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Persists changes to a previously loaded aggregate. Caller must SaveChangesAsync.</summary>
    Task UpdateAsync(CatalogSyncRun run, CancellationToken cancellationToken = default);
}
