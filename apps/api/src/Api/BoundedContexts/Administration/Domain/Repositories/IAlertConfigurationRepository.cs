using Api.BoundedContexts.Administration.Domain.Aggregates.AlertConfigurations;

namespace Api.BoundedContexts.Administration.Domain.Repositories;

/// <summary>
/// Repository for AlertConfiguration aggregate (Issue #921)
/// </summary>
public interface IAlertConfigurationRepository
{
    Task<AlertConfiguration?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<AlertConfiguration?> GetByKeyAsync(string configKey, CancellationToken ct = default);
    Task<List<AlertConfiguration>> GetByCategoryAsync(ConfigCategory category, CancellationToken ct = default);
    Task<List<AlertConfiguration>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(AlertConfiguration config, CancellationToken ct = default);
    Task UpdateAsync(AlertConfiguration config, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
