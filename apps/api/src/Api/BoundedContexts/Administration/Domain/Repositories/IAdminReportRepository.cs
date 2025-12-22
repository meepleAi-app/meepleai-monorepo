using Api.BoundedContexts.Administration.Domain.Entities;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for AdminReport aggregate
/// ISSUE-916: Report definition persistence
/// </summary>
internal interface IAdminReportRepository
{
    Task<AdminReport?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminReport>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AdminReport>> GetActiveScheduledReportsAsync(CancellationToken cancellationToken = default);
    Task AddAsync(AdminReport report, CancellationToken cancellationToken = default);
    Task UpdateAsync(AdminReport report, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

