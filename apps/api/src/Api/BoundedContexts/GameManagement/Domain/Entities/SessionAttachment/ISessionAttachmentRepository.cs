namespace Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;

/// <summary>
/// Repository interface for session attachment persistence.
/// Issue #5359 - SessionAttachment domain entity.
/// </summary>
internal interface ISessionAttachmentRepository
{
    Task<SessionAttachment?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionAttachment>> GetBySessionIdAsync(
        Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionAttachment>> GetBySnapshotAsync(
        Guid sessionId, int snapshotIndex, CancellationToken cancellationToken = default);

    Task<int> CountByPlayerAndSnapshotAsync(
        Guid sessionId, Guid playerId, int? snapshotIndex, CancellationToken cancellationToken = default);

    Task AddAsync(SessionAttachment attachment, CancellationToken cancellationToken = default);

    Task<bool> SoftDeleteAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionAttachment>> GetExpiredAttachmentsAsync(
        DateTime cutoffDate, int batchSize, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
