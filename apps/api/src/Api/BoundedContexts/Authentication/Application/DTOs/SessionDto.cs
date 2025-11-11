namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Data transfer object for session information.
/// </summary>
public record SessionDto(
    Guid Id,
    Guid UserId,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? LastSeenAt,
    DateTime? RevokedAt,
    string? IpAddress,
    string? UserAgent,
    bool IsActive
);

/// <summary>
/// DTO for session status check.
/// </summary>
public record SessionStatusDto(
    bool IsValid,
    UserDto? User,
    DateTime? ExpiresAt
);
