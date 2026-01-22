using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

/// <summary>
/// Repository interface for managing ShareRequestLimitConfig entities.
/// Provides methods for retrieving and persisting rate limit configurations by tier.
/// </summary>
public interface IRateLimitConfigRepository : IRepository<ShareRequestLimitConfig, Guid>
{
    /// <summary>
    /// Retrieves the rate limit configuration for a specific user tier.
    /// </summary>
    /// <param name="tier">The user tier to retrieve configuration for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The rate limit configuration for the specified tier, or null if not found.</returns>
    Task<ShareRequestLimitConfig?> GetByTierAsync(
        UserTier tier,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active rate limit configurations.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of all active configurations.</returns>
    Task<IReadOnlyList<ShareRequestLimitConfig>> GetAllActiveAsync(
        CancellationToken cancellationToken = default);

}
