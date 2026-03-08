using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.DocumentProcessing.Domain.Repositories;

/// <summary>
/// Repository interface for ProcessingJob aggregate.
/// Issue #4731: Queue commands/queries.
/// </summary>
internal interface IProcessingJobRepository : IRepository<ProcessingJob, Guid>
{
    Task<int> CountByStatusAsync(JobStatus status, CancellationToken cancellationToken = default);
    Task<bool> ExistsByPdfDocumentIdAsync(Guid pdfDocumentId, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<ProcessingJob> Items, int Total)> GetPaginatedAsync(
        JobStatus? statusFilter,
        string? searchText,
        DateTimeOffset? fromDate,
        DateTimeOffset? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<ProcessingJob?> GetByIdWithDetailsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically dequeue the next highest-priority job (priority DESC, CreatedAt ASC).
    /// Returns null if no queued jobs exist.
    /// Issue #5455: Priority-based dequeue.
    /// </summary>
    Task<ProcessingJob?> DequeueNextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Count currently processing jobs.
    /// Issue #5455: Concurrency control.
    /// </summary>
    Task<int> CountProcessingAsync(CancellationToken cancellationToken = default);
}
