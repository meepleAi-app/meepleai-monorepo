using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
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
