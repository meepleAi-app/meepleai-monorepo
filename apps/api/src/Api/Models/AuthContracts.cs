namespace Api.Models;

public record RegisterPayload(
    string Email,
    string Password,
    string? DisplayName,
    string? Role);

public class LoginPayload
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public record RegisterCommand(
    string Email,
    string Password,
    string? DisplayName,
    string? Role,
    string? IpAddress,
    string? UserAgent);

public record LoginCommand(
    string Email,
    string Password,
    string? IpAddress,
    string? UserAgent);

public record AuthUser(
    string Id,
    string Email,
    string? DisplayName,
    string Role);

public record AuthResult(AuthUser User, string SessionToken, DateTime ExpiresAt);

public record ActiveSession(AuthUser User, DateTime ExpiresAt, DateTime? LastSeenAt);

public record AuthResponse(AuthUser User, DateTime? ExpiresAt);

public record SessionInfo(
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
public record SessionStatusResponse(
    DateTime ExpiresAt,
    DateTime? LastSeenAt,
    int RemainingMinutes);

public class SessionManagementConfiguration
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

// AUTH-04: Password reset models
public record PasswordResetRequestPayload(string Email);
public record PasswordResetConfirmPayload(string Token, string NewPassword);

// AUTH-06: OAuth models
/// <summary>
/// OAuth account information returned to frontend
/// </summary>
public record OAuthAccountDto(
    string Provider,
    DateTime CreatedAt);

/// <summary>
/// Internal result from OAuth callback processing
/// </summary>
public record OAuthCallbackResult(
    AuthUser User,
    bool IsNewUser);

/// <summary>
/// OAuth provider configuration
/// </summary>
public class OAuthProviderConfig
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
public record OAuthTokenResponse(
    string AccessToken,
    string? RefreshToken,
    int? ExpiresIn,
    string TokenType);

/// <summary>
/// User information from OAuth provider
/// </summary>
public record OAuthUserInfo(
    string Id,
    string Email,
    string? Name);

/// <summary>
/// OAuth configuration section
/// </summary>
public class OAuthConfiguration
{
    public required string CallbackBaseUrl { get; init; }
    public required Dictionary<string, OAuthProviderConfig> Providers { get; init; }
}
