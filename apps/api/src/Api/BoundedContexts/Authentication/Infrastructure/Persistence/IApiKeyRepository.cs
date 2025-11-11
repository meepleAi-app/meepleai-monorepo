using Api.BoundedContexts.Authentication.Domain.Entities;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Repository interface for ApiKey entity.
/// Note: ApiKey is not an aggregate root, but we provide repository for query convenience.
/// API keys are managed through the User aggregate root.
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>
    /// Finds an API key by its key prefix (first 8 characters).
    /// </summary>
    Task<ApiKey?> GetByKeyPrefixAsync(string keyPrefix, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all API keys for a specific user.
    /// </summary>
    Task<List<ApiKey>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active (valid) API keys for a user.
    /// </summary>
    Task<List<ApiKey>> GetActiveKeysByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new API key.
    /// </summary>
    Task AddAsync(ApiKey apiKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing API key.
    /// </summary>
    Task UpdateAsync(ApiKey apiKey, CancellationToken cancellationToken = default);
}
