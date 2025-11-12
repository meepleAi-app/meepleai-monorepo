namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// User profile data transfer object.
/// Extended version of UserDto with profile-specific information.
/// </summary>
public record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAt,
    bool IsTwoFactorEnabled,
    DateTime? TwoFactorEnabledAt
);
