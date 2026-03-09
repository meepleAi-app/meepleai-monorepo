using System.Text.Json.Serialization;

#pragma warning disable MA0048 // File name must match type name - Multi-type DTO file
namespace Api.Models;

/// <summary>
/// Registration payload supporting both camelCase (frontend) and PascalCase (backend) JSON formats.
/// Case-insensitive matching is configured globally in Program.cs via ConfigureHttpJsonOptions.
/// </summary>
internal class RegisterPayload
{
    /// <summary>
    /// User email address. Accepts both "email" (camelCase) and "Email" (PascalCase) in JSON.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User password. Accepts both "password" (camelCase) and "Password" (PascalCase) in JSON.
    /// </summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>
    /// Optional display name. Accepts both "displayName" (camelCase) and "DisplayName" (PascalCase) in JSON.
    /// If not provided, defaults to email prefix in endpoint handler.
    /// </summary>
    public string? DisplayName { get; set; }

}

/// <summary>
/// Login payload supporting both camelCase (frontend) and PascalCase (backend) JSON formats.
/// Case-insensitive matching is configured globally in Program.cs via ConfigureHttpJsonOptions.
/// </summary>
internal class LoginPayload
{
    /// <summary>
    /// User email address. Accepts both "email" (camelCase) and "Email" (PascalCase) in JSON.
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// User password. Accepts both "password" (camelCase) and "Password" (PascalCase) in JSON.
    /// </summary>
    public string Password { get; set; } = string.Empty;
}

internal record LoginCommand(
    string Email,
    string Password,
    string? IpAddress,
    string? UserAgent);

internal record AuthUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("role")] string Role);

internal record AuthResult(AuthUser User, string SessionToken, DateTime ExpiresAt);

internal record AuthResponse(
    [property: JsonPropertyName("user")] AuthUser User,
    [property: JsonPropertyName("expiresAt")] DateTime? ExpiresAt);

internal record SessionInfo(
    string Id,
    string UserId,
    string UserEmail,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastSeenAt,
    DateTime? RevokedAt,
    string? IpAddress,
    string? UserAgent);

/// <summary>
/// Response for session status check (AUTH-05)
/// </summary>
internal record SessionStatusResponse(
    DateTime ExpiresAt,
    DateTime? LastSeenAt,
    int RemainingMinutes);

internal class SessionManagementConfiguration
{
    /// <summary>
    /// Number of days of inactivity before a session is auto-revoked.
    /// Default: 30 days.
    /// </summary>
    public int InactivityTimeoutDays { get; set; } = 30;

    /// <summary>
    /// Interval in hours between auto-revocation checks.
    /// Default: 1 hour.
    /// </summary>
    public int AutoRevocationIntervalHours { get; set; } = 1;
}

// API Key authentication models
/// <summary>
/// API key login payload for browser-based API key authentication.
/// </summary>
internal record ApiKeyLoginPayload(string ApiKey);

// AUTH-04: Password reset models
internal record PasswordResetRequestPayload(string Email);
internal record PasswordResetConfirmPayload(string Token, string NewPassword);

// AUTH-06: OAuth models
/// <summary>
/// OAuth account information returned to frontend
/// </summary>
internal record OAuthAccountDto(
    string Provider,
    DateTime CreatedAt);

/// <summary>
/// Internal result from OAuth callback processing
/// </summary>
internal record OAuthCallbackResult(
    AuthUser User,
    bool IsNewUser);

/// <summary>
/// OAuth provider configuration
/// </summary>
internal class OAuthProviderConfig
{
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string AuthorizationUrl { get; init; }
    public required string TokenUrl { get; init; }
    public required string UserInfoUrl { get; init; }
    public required string Scope { get; init; }
}

/// <summary>
/// OAuth token response from provider
/// </summary>
internal record OAuthTokenResponse(
    string AccessToken,
    string? RefreshToken,
    int? ExpiresIn,
    string TokenType);

/// <summary>
/// User information from OAuth provider
/// </summary>
internal record OAuthUserInfo(
    string Id,
    string Email,
    string? Name);

/// <summary>
/// OAuth configuration section
/// </summary>
internal class OAuthConfiguration
{
    public required string CallbackBaseUrl { get; init; }
    public required IDictionary<string, OAuthProviderConfig> Providers { get; init; }
}

/// <summary>
/// Payload for logout-all-devices endpoint (Issue #2056).
/// Allows user to revoke all active sessions with optional password verification.
/// </summary>
/// <param name="IncludeCurrentSession">Whether to also revoke the current session (requires user confirmation). If false, the current session is preserved and the user remains logged in.</param>
/// <param name="Password">Optional password for additional security verification. When provided, the password is verified before revoking sessions.</param>
internal record LogoutAllDevicesPayload(
    bool IncludeCurrentSession = false,
    string? Password = null);
