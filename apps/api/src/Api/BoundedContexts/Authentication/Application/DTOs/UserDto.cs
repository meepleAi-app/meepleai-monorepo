namespace Api.BoundedContexts.Authentication.Application.DTOs;

/// <summary>
/// Data transfer object for user information.
/// </summary>
public record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    DateTime CreatedAt,
    bool IsTwoFactorEnabled,
    DateTime? TwoFactorEnabledAt
);

/// <summary>
/// DTO for user registration.
/// </summary>
public record RegisterUserRequest(
    string Email,
    string Password,
    string DisplayName,
    string Role = "user"
);

/// <summary>
/// DTO for user registration response.
/// </summary>
public record RegisterUserResponse(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role
);

/// <summary>
/// DTO for login request.
/// </summary>
public record LoginRequest(
    string Email,
    string Password
);

/// <summary>
/// DTO for login response.
/// </summary>
public record LoginResponse(
    bool RequiresTwoFactor,
    string? TempSessionToken,
    UserDto? User,
    string? SessionToken
);

/// <summary>
/// DTO for 2FA verification request.
/// </summary>
public record Verify2FARequest(
    string TempSessionToken,
    string Code
);

/// <summary>
/// DTO for 2FA verification response.
/// </summary>
public record Verify2FAResponse(
    UserDto User,
    string SessionToken
);
