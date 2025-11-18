using System;
using System.Threading.Tasks;

namespace Api.BoundedContexts.Authentication.Application.Interfaces;

/// <summary>
/// Provides storage and validation for OAuth CSRF state tokens.
/// Implementations must ensure thread-safety and support distributed deployments.
/// </summary>
public interface IOAuthStateStore
{
    /// <summary>
    /// Stores an OAuth state token with expiration.
    /// </summary>
    /// <param name="state">The OAuth state token (CSRF protection)</param>
    /// <param name="expiration">Time-to-live for the state token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task StoreStateAsync(string state, TimeSpan expiration);

    /// <summary>
    /// Validates and removes an OAuth state token (single-use).
    /// </summary>
    /// <param name="state">The OAuth state token to validate</param>
    /// <returns>True if state is valid and was removed, false otherwise</returns>
    Task<bool> ValidateAndRemoveStateAsync(string state);

    /// <summary>
    /// Checks if an OAuth state token exists without removing it.
    /// </summary>
    /// <param name="state">The OAuth state token to check</param>
    /// <returns>True if state exists and is not expired, false otherwise</returns>
    Task<bool> ExistsAsync(string state);

    /// <summary>
    /// Removes expired state tokens (cleanup operation).
    /// </summary>
    /// <returns>Number of expired tokens removed</returns>
    Task<int> CleanupExpiredStatesAsync();
}
