using Api.Models;

namespace Api.Services;

/// <summary>
/// Infrastructure adapter for OAuth 2.0 provider HTTP communication (Google, Discord, GitHub).
/// Business logic delegated to CQRS handlers.
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

    /// <summary>
    /// Exchanges authorization code for access token (infrastructure adapter)
    /// </summary>
    /// <param name="provider">OAuth provider (google, discord, github)</param>
    /// <param name="code">Authorization code from provider</param>
    /// <returns>Token response from provider</returns>
    Task<OAuthTokenResponse> ExchangeCodeForTokenAsync(string provider, string code);

    /// <summary>
    /// Gets user info from OAuth provider using access token (infrastructure adapter)
    /// </summary>
    /// <param name="provider">OAuth provider (google, discord, github)</param>
    /// <param name="accessToken">Access token from provider</param>
    /// <returns>User info from provider</returns>
    Task<OAuthUserInfo> GetUserInfoAsync(string provider, string accessToken);
}
