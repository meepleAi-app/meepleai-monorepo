using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

/// <summary>
/// Repository interface for ChunkedUploadSession aggregate.
/// </summary>
public interface IChunkedUploadSessionRepository : IRepository<ChunkedUploadSession, Guid>
{
    /// <summary>
    /// Finds all active (non-expired, non-completed) sessions for a user.
    /// </summary>
    Task<IReadOnlyList<ChunkedUploadSession>> FindActiveByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all expired sessions that need cleanup.
    /// </summary>
    Task<IReadOnlyList<ChunkedUploadSession>> FindExpiredSessionsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds sessions by status.
    /// </summary>
    Task<IReadOnlyList<ChunkedUploadSession>> FindByStatusAsync(
        string status,
        CancellationToken cancellationToken = default);
}
