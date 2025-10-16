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
