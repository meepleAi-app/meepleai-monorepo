namespace Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;

/// <summary>
/// Repository interface for SessionSnapshot persistence.
/// Issue #4755: SessionSnapshot - Delta-based History + State Reconstruction.
/// </summary>
internal interface ISessionSnapshotRepository
{
    Task<SessionSnapshot?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionSnapshot>> GetBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task<SessionSnapshot?> GetBySessionAndIndexAsync(Guid sessionId, int snapshotIndex, CancellationToken cancellationToken = default);
    Task<SessionSnapshot?> GetLatestBySessionIdAsync(Guid sessionId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets snapshots from the nearest checkpoint up to (and including) the target index.
    /// Used for state reconstruction: checkpoint + deltas.
    /// </summary>
    Task<IReadOnlyList<SessionSnapshot>> GetSnapshotsForReconstructionAsync(
        Guid sessionId, int targetIndex, CancellationToken cancellationToken = default);

    Task<int> GetSnapshotCountAsync(Guid sessionId, CancellationToken cancellationToken = default);
    Task AddAsync(SessionSnapshot snapshot, CancellationToken cancellationToken = default);
}
