using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Command with Result record
namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to enable two-factor authentication for a user.
/// Requires verification of TOTP code before enabling.
/// DDD CQRS: Uses TotpCode parameter (matching handler).
/// </summary>
internal record Enable2FACommand(
    Guid UserId,
    string TotpCode
) : ICommand<Enable2FAResult>;

/// <summary>
/// Result of enabling 2FA.
/// </summary>
internal sealed record Enable2FAResult(
    bool Success,
    IList<string>? BackupCodes = null,
    string? ErrorMessage = null
);
