

#pragma warning disable MA0048 // File name must match type name - Contains related Request/Response DTOs
namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Data transfer object for user information.
/// </summary>
internal record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    string Tier,
    DateTime CreatedAt,
    bool IsTwoFactorEnabled,
    DateTime? TwoFactorEnabledAt,
    int Level,
    int ExperiencePoints
);

/// <summary>
/// DTO for user registration.
/// </summary>
internal record RegisterUserRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role = "user"
);

/// <summary>
/// DTO for user registration response.
/// </summary>
internal record RegisterUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role
);

/// <summary>
/// DTO for login request.
/// </summary>
internal record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// DTO for login response.
/// </summary>
internal record LoginResponse(
    bool RequiresTwoFactor,
    string? TempSessionToken,
    UserDto? User,
    string? SessionToken
);

/// <summary>
/// DTO for 2FA verification request.
/// </summary>
internal record Verify2FARequest(
    string TempSessionToken,
    string Code
);

/// <summary>
/// DTO for 2FA verification response.
/// </summary>
internal record Verify2FAResponse(
    UserDto User,
    string SessionToken
);
