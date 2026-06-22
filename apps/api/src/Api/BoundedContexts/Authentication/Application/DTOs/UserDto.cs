

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
    int ExperiencePoints,
    bool EmailVerified = false,                        // Issue #3672 (default for backward compatibility)
    DateTime? EmailVerifiedAt = null,                  // Issue #3672
    DateTime? VerificationGracePeriodEndsAt = null,    // Issue #3672
    bool OnboardingCompleted = false,                  // Issue #323
    bool OnboardingSkipped = false                     // Issue #323
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
///
/// I2 / F5 (auth security fixes): <see cref="ExpiresAt"/> is the canonical
/// session expiration. On the success branch (RequiresTwoFactor=false,
/// SessionToken non-null) it MUST mirror the Session aggregate's ExpiresAt
/// so the endpoint stops recomputing it from configuration. On the 2FA
/// branch (no full session minted yet) the value is the temp-session
/// expiration, which is what the client actually needs to know to time
/// out the in-progress flow.
///
/// Non-nullable: every code path that constructs a LoginResponse must
/// supply a real value. Pre-fix the field was DateTime? with a default
/// of null; the endpoint had a 30-day-AddDays fallback that could
/// silently disagree with the actual session row when the field happened
/// to be null. Making this required eliminates the fallback path.
/// </summary>
internal record LoginResponse(
    bool RequiresTwoFactor,
    string? TempSessionToken,
    UserDto? User,
    string? SessionToken,
    DateTime ExpiresAt
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
