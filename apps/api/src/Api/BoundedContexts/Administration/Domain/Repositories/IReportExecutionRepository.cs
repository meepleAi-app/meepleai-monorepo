using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for ReportExecution aggregate
/// ISSUE-916: Report execution history persistence
/// </summary>
internal interface IReportExecutionRepository
{
    Task<ReportExecution?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ReportExecution>> GetByReportIdAsync(Guid reportId, int limit = 50, CancellationToken ct = default);
    Task<IReadOnlyList<ReportExecution>> GetRecentExecutionsAsync(int limit = 100, CancellationToken ct = default);
    Task AddAsync(ReportExecution execution, CancellationToken ct = default);
    Task UpdateAsync(ReportExecution execution, CancellationToken ct = default);
}
