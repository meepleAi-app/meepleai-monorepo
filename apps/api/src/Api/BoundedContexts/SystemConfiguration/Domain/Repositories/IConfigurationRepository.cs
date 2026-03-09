using Api.SharedKernel.Infrastructure.Persistence;
using SystemConfigurationEntity = Api.BoundedContexts.SystemConfiguration.Domain.Entities.SystemConfiguration;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

/// <summary>
/// Repository interface for SystemConfiguration aggregate.
/// Made public to enable Moq proxy generation in unit tests (Issue #2188).
/// </summary>
public interface IConfigurationRepository : IRepository<SystemConfigurationEntity, Guid>
{
    Task<SystemConfigurationEntity?> GetByKeyAsync(string key, string? environment = null, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemConfigurationEntity>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemConfigurationEntity>> GetByKeysAsync(IEnumerable<string> keys, string? environment = null, bool activeOnly = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemConfigurationEntity>> GetByCategoryAsync(string category, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemConfigurationEntity>> GetActiveConfigurationsAsync(CancellationToken cancellationToken = default);
}
