

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
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
    DateTime? ExpiresAt,
    DateTime? LastSeenAt
);
