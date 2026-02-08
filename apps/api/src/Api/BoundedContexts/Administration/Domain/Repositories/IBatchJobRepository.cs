using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for BatchJob aggregate (Issue #3693)
/// </summary>
internal interface IBatchJobRepository
{
    Task<BatchJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<BatchJob>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<BatchJob>> GetByStatusAsync(JobStatus status, CancellationToken cancellationToken = default);
    Task<List<BatchJob>> GetPagedAsync(int skip, int take, JobStatus? status = null, CancellationToken cancellationToken = default);
    Task<int> CountByStatusAsync(JobStatus? status = null, CancellationToken cancellationToken = default);
    Task AddAsync(BatchJob batchJob, CancellationToken cancellationToken = default);
    Task UpdateAsync(BatchJob batchJob, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
