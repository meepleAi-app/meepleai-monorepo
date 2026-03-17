namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// User profile data transfer object.
/// Extended version of UserDto with profile-specific information including preferences.
/// </summary>
internal record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAt,
    bool IsTwoFactorEnabled,
    DateTime? TwoFactorEnabledAt,
    string Language,
    string Theme,
    bool EmailNotifications,
    int DataRetentionDays,
    bool ShowProfile = true,
    bool ShowActivity = true,
    bool ShowLibrary = true
);
