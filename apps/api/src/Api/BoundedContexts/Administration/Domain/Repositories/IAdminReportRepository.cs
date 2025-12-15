using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for AdminReport aggregate
/// ISSUE-916: Report definition persistence
/// </summary>
internal interface IAdminReportRepository
{
    Task<AdminReport?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<AdminReport>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AdminReport>> GetActiveScheduledReportsAsync(CancellationToken ct = default);
    Task AddAsync(AdminReport report, CancellationToken ct = default);
    Task UpdateAsync(AdminReport report, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
