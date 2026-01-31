using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;

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

    // Issue #2596: Tier routing queries
    /// <summary>
    /// Get the default model configuration for a specific user tier and environment.
    /// </summary>
    Task<AiModelConfiguration?> GetDefaultForTierAsync(
        LlmUserTier tier,
        LlmEnvironmentType environment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all model configurations for a specific user tier.
    /// </summary>
    Task<IReadOnlyList<AiModelConfiguration>> GetByTierAsync(
        LlmUserTier tier,
        LlmEnvironmentType? environment = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all tier routing configurations (for admin overview).
    /// </summary>
    Task<IReadOnlyList<AiModelConfiguration>> GetAllTierRoutingsAsync(
        CancellationToken cancellationToken = default);
}
