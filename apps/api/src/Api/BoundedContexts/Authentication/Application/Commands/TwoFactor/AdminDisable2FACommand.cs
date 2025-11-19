using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.TwoFactor;

/// <summary>
/// Command to disable two-factor authentication for a user via admin override.
/// DDD CQRS: Admin-only operation for users who lost authenticator + backup codes.
/// Does not require password or 2FA code verification.
/// </summary>
public sealed record AdminDisable2FACommand(
    Guid AdminUserId,
    Guid TargetUserId
) : ICommand<AdminDisable2FAResult>;

/// <summary>
/// Result of admin 2FA disable operation.
/// </summary>
public sealed record AdminDisable2FAResult(
    bool Success,
    string? ErrorMessage = null
);
