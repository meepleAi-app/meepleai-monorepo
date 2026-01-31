using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Domain.Repositories;

/// <summary>
/// Repository interface for managing UserRateLimitOverride entities.
/// Provides methods for retrieving and persisting user-specific rate limit overrides.
/// </summary>
public interface IUserRateLimitOverrideRepository : IRepository<UserRateLimitOverride, Guid>
{
    /// <summary>
    /// Retrieves the active rate limit override for a specific user.
    /// Only returns non-expired overrides.
    /// </summary>
    /// <param name="userId">The user ID to retrieve override for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The active override for the user, or null if none exists or expired.</returns>
    Task<UserRateLimitOverride?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user has an active (non-expired) rate limit override.
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user has an active override, false otherwise.</returns>
    Task<bool> HasActiveOverrideAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all active (non-expired) rate limit overrides.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of all active overrides.</returns>
    Task<IReadOnlyList<UserRateLimitOverride>> GetAllActiveAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all rate limit overrides for a specific user, including expired ones.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A read-only list of all overrides for the user.</returns>
    Task<List<UserRateLimitOverride>> GetAllByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
