using Api.BoundedContexts.GameManagement.Domain.Entities;

namespace Api.BoundedContexts.GameManagement.Domain.Repositories;

/// <summary>
/// Repository interface for PlayRecordVersion persistence.
/// #2437-3: version history + restore for play records.
/// </summary>
internal interface IPlayRecordVersionRepository
{
    /// <summary>
    /// Returns the next monotonic version number for the given record.
    /// Returns 1 if no versions exist yet.
    /// </summary>
    Task<int> GetNextVersionNumberAsync(Guid playRecordId, CancellationToken ct = default);

    /// <summary>
    /// Persists a new version snapshot. Does not call SaveChanges — the UnitOfWork saves.
    /// </summary>
    Task AddAsync(PlayRecordVersion version, CancellationToken ct = default);

    /// <summary>
    /// Returns the <paramref name="limit"/> most-recent versions for a record, ordered by
    /// VersionNumber descending. Results are read-only (no tracking).
    /// </summary>
    Task<IReadOnlyList<PlayRecordVersion>> GetRecentAsync(Guid playRecordId, int limit, CancellationToken ct = default);

    /// <summary>
    /// Returns a specific version by its number, or null if not found.
    /// </summary>
    Task<PlayRecordVersion?> GetByVersionNumberAsync(Guid playRecordId, int versionNumber, CancellationToken ct = default);

    /// <summary>
    /// Deletes versions beyond the <paramref name="keep"/> most-recent ones.
    /// Does not call SaveChanges — the UnitOfWork saves.
    /// </summary>
    Task PruneOldestAsync(Guid playRecordId, int keep, CancellationToken ct = default);
}
