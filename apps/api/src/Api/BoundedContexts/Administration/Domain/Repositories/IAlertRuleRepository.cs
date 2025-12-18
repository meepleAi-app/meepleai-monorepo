using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for AlertRule aggregate (Issue #921)
/// </summary>
internal interface IAlertRuleRepository
{
    Task<AlertRule?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AlertRule?> GetByNameAsync(string name, CancellationToken cancellationToken = default);
    Task<List<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<List<AlertRule>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task<List<AlertRule>> GetByAlertTypeAsync(string alertType, CancellationToken cancellationToken = default);
    Task AddAsync(AlertRule alertRule, CancellationToken cancellationToken = default);
    Task UpdateAsync(AlertRule alertRule, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

