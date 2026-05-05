using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

/// <summary>
/// Repository interface for <see cref="PhotoBatchUpload"/> aggregate.
/// Libro Game AI Assistant MVP Phase 1.
/// </summary>
internal interface IPhotoBatchUploadRepository : IRepository<PhotoBatchUpload, Guid>
{
    /// <summary>
    /// Finds all batches belonging to a user, ordered by most recent first.
    /// </summary>
    Task<IReadOnlyList<PhotoBatchUpload>> FindByUserIdAsync(Guid userId, CancellationToken ct = default);

    /// <summary>
    /// Loads a batch together with its pages (eager-loaded).
    /// Returns null when not found.
    /// </summary>
    Task<PhotoBatchUpload?> FindByIdWithPagesAsync(Guid id, CancellationToken ct = default);
}
