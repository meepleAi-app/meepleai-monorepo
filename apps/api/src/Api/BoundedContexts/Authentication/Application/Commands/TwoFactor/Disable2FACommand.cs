using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to disable two-factor authentication for a user.
/// DDD CQRS: Command with business logic delegated to User aggregate.
/// Requires both password and 2FA code for security.
/// </summary>
public sealed record Disable2FACommand(
    Guid UserId,
    string CurrentPassword,
    string TotpOrBackupCode
) : ICommand<Disable2FAResult>;

/// <summary>
/// Result of disabling 2FA.
/// </summary>
public sealed record Disable2FAResult(
    bool Success,
    string? ErrorMessage = null
);
