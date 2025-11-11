using Api.Models;

namespace Api.Services;

/// <summary>
/// Handles OAuth 2.0 authentication flows for Google, Discord, and GitHub.
/// </summary>
public interface IOAuthService
{
    /// <summary>
    /// Generates the OAuth authorization URL with CSRF state protection
    /// </summary>
    /// <param name="provider">OAuth provider (google, discord, github)</param>
    /// <param name="state">CSRF protection state token</param>
    /// <returns>Complete authorization URL for redirect</returns>
    Task<string> GetAuthorizationUrlAsync(string provider, string state);

    /// <summary>
    /// Handles OAuth callback after user authorizes
    /// </summary>
    /// <param name="provider">OAuth provider</param>
    /// <param name="code">Authorization code from provider</param>
    /// <param name="state">CSRF state token for validation</param>
    /// <returns>User and whether they are newly created</returns>
    Task<OAuthCallbackResult> HandleCallbackAsync(string provider, string code, string state);

    /// <summary>
    /// Unlinks an OAuth account from the user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">OAuth provider to unlink</param>
    Task UnlinkOAuthAccountAsync(Guid userId, string provider);

    /// <summary>
    /// Gets all linked OAuth accounts for a user
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <returns>List of linked OAuth accounts</returns>
    Task<List<OAuthAccountDto>> GetLinkedAccountsAsync(Guid userId);

    /// <summary>
    /// Validates CSRF state token
    /// </summary>
    /// <param name="state">State token to validate</param>
    /// <returns>True if valid, false otherwise</returns>
    Task<bool> ValidateStateAsync(string state);

    /// <summary>
    /// Stores CSRF state token for validation
    /// </summary>
    /// <param name="state">State token to store</param>
    Task StoreStateAsync(string state);

    /// <summary>
    /// Refreshes an expired OAuth access token using refresh token (Google, Discord only)
    /// </summary>
    /// <param name="userId">User ID</param>
    /// <param name="provider">OAuth provider (google, discord)</param>
    /// <returns>New token response, or null if refresh fails (force re-auth)</returns>
    Task<OAuthTokenResponse?> RefreshTokenAsync(Guid userId, string provider);
}
