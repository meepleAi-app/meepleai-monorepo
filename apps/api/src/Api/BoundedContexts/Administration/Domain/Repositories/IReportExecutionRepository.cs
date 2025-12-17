using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for ReportExecution aggregate
/// ISSUE-916: Report execution history persistence
/// </summary>
internal interface IReportExecutionRepository
{
    Task<ReportExecution?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportExecution>> GetByReportIdAsync(Guid reportId, int limit = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportExecution>> GetRecentExecutionsAsync(int limit = 100, CancellationToken cancellationToken = default);
    Task AddAsync(ReportExecution execution, CancellationToken cancellationToken = default);
    Task UpdateAsync(ReportExecution execution, CancellationToken cancellationToken = default);
}

