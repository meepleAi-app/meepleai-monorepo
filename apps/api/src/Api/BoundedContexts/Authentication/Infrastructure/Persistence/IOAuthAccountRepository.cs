using Api.BoundedContexts.Authentication.Domain.Entities;

namespace Api.BoundedContexts.Authentication.Infrastructure.Persistence;

/// <summary>
/// Repository interface for OAuthAccount entity.
/// Provides data access operations specific to OAuth linked accounts.
/// Note: OAuthAccount is not an aggregate root - it belongs to the User aggregate.
/// This repository provides specialized queries for OAuth account management.
/// </summary>
internal interface IOAuthAccountRepository
{
    /// <summary>
    /// Gets an OAuth account by its ID.
    /// </summary>
    /// <param name="id">The OAuth account ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The OAuth account if found, null otherwise</returns>
    Task<OAuthAccount?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all OAuth accounts in the system.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all OAuth accounts</returns>
    Task<List<OAuthAccount>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new OAuth account.
    /// </summary>
    /// <param name="entity">The OAuth account to add</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task AddAsync(OAuthAccount entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing OAuth account.
    /// </summary>
    /// <param name="entity">The OAuth account to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateAsync(OAuthAccount entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an OAuth account.
    /// </summary>
    /// <param name="entity">The OAuth account to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteAsync(OAuthAccount entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an OAuth account exists by ID.
    /// </summary>
    /// <param name="id">The OAuth account ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the account exists, false otherwise</returns>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an OAuth account by user ID and provider.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="provider">The OAuth provider (google, discord, github)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The OAuth account if found, null otherwise</returns>
    Task<OAuthAccount?> GetByUserIdAndProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds all OAuth accounts linked to a specific user.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of OAuth accounts linked to the user</returns>
    Task<IReadOnlyList<OAuthAccount>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds an OAuth account by provider and provider user ID.
    /// Used during OAuth login to find existing accounts.
    /// </summary>
    /// <param name="provider">The OAuth provider (google, discord, github)</param>
    /// <param name="providerUserId">The user ID from the OAuth provider</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The OAuth account if found, null otherwise</returns>
    Task<OAuthAccount?> GetByProviderUserIdAsync(string provider, string providerUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if an OAuth account exists for a user and provider combination.
    /// </summary>
    /// <param name="userId">The user ID</param>
    /// <param name="provider">The OAuth provider (google, discord, github)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if the account exists, false otherwise</returns>
    Task<bool> ExistsByUserIdAndProviderAsync(Guid userId, string provider, CancellationToken cancellationToken = default);
}
