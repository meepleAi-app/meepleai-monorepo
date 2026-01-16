using Api.BoundedContexts.SystemConfiguration.Domain.Entities;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

public interface IAiModelConfigurationRepository
{
    Task<AiModelConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<AiModelConfiguration?> GetByModelIdAsync(string modelId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiModelConfiguration>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AiModelConfiguration>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<AiModelConfiguration?> GetPrimaryAsync(CancellationToken cancellationToken = default);
    Task<bool> AnyAsync(CancellationToken cancellationToken = default);
    Task AddAsync(AiModelConfiguration entity, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<AiModelConfiguration> entities, CancellationToken cancellationToken = default);
    Task UpdateAsync(AiModelConfiguration entity, CancellationToken cancellationToken = default);
    Task DeleteAsync(AiModelConfiguration entity, CancellationToken cancellationToken = default);
}
