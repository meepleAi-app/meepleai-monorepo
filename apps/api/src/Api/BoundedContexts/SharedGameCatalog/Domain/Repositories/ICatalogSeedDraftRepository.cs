using Api.Infrastructure.Entities.SharedGameCatalog;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;

/// <summary>
/// Repository interface for <see cref="CatalogSeedDraftEntity"/>.
/// Admin-curated draft entries from the catalog seed import workflow.
/// Lifecycle: Pending → Fetched → Approved or Rejected (or FetchFailed).
/// Issue #1903 — M1.5.
/// </summary>
public interface ICatalogSeedDraftRepository
{
    /// <summary>
    /// Gets a single draft by its ID.
    /// </summary>
    Task<CatalogSeedDraftEntity?> GetByIdAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Fetches up to <paramref name="take"/> drafts with the given status, ordered by
    /// CreatedAt ASC (FIFO). **Entities are returned tracked** because callers
    /// (notably the CatalogSeedFetchJob) mutate them in the same DbContext scope
    /// and save the changes. For read-only displays use <see cref="ListAsync"/>.
    /// </summary>
    Task<IReadOnlyList<CatalogSeedDraftEntity>> GetByStatusAsync(string status, int take, CancellationToken ct);

    /// <summary>
    /// Lists drafts with optional status filter, ordered by most recent first.
    /// Used by admin UI pagination.
    /// </summary>
    Task<IReadOnlyList<CatalogSeedDraftEntity>> ListAsync(
        string? statusFilter, int skip, int take, CancellationToken ct);

    /// <summary>
    /// Counts drafts matching the optional status filter.
    /// Used for pagination metadata.
    /// </summary>
    Task<int> CountAsync(string? statusFilter, CancellationToken ct);

    /// <summary>
    /// Adds a single new draft to the change tracker.
    /// </summary>
    Task AddAsync(CatalogSeedDraftEntity entity, CancellationToken ct);

    /// <summary>
    /// Adds multiple drafts to the change tracker (batch enqueue).
    /// </summary>
    Task AddRangeAsync(IReadOnlyList<CatalogSeedDraftEntity> entities, CancellationToken ct);

    /// <summary>
    /// Marks an existing draft as modified for the change tracker.
    /// </summary>
    void Update(CatalogSeedDraftEntity entity);

    /// <summary>
    /// Returns whether a draft exists for the given BGG ID. Used by enqueue
    /// idempotency checks.
    /// </summary>
    Task<bool> ExistsAsync(int bggId, CancellationToken ct);
}
