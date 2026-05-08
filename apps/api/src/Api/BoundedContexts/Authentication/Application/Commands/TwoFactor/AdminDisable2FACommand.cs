using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Command to disable two-factor authentication for a user via admin override.
/// DDD CQRS: Admin-only operation for users who lost authenticator + backup codes.
/// I4 (auth security fixes): the admin must supply their own current password
/// — re-authentication on a high-impact action is required even when the
/// admin already holds a valid session cookie.
/// </summary>
internal sealed record AdminDisable2FACommand(
    Guid AdminUserId,
    Guid TargetUserId,
    string AdminPassword
) : ICommand<AdminDisable2FAResult>;

/// <summary>
/// Result of admin 2FA disable operation.
/// </summary>
internal sealed record AdminDisable2FAResult(
    bool Success,
    string? ErrorMessage = null
);
