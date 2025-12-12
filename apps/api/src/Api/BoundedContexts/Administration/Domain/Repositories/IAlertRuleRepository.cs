using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for AlertRule aggregate (Issue #921)
/// </summary>
public interface IAlertRuleRepository
{
    Task<AlertRule?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AlertRule?> GetByNameAsync(string name, CancellationToken ct = default);
    Task<List<AlertRule>> GetAllAsync(CancellationToken ct = default);
    Task<List<AlertRule>> GetEnabledAsync(CancellationToken ct = default);
    Task<List<AlertRule>> GetByAlertTypeAsync(string alertType, CancellationToken ct = default);
    Task AddAsync(AlertRule alertRule, CancellationToken ct = default);
    Task UpdateAsync(AlertRule alertRule, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
