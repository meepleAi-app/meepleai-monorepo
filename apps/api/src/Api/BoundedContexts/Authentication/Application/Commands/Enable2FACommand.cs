using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Command to enable two-factor authentication for a user.
/// Requires verification of TOTP code before enabling.
/// </summary>
public record Enable2FACommand(
    Guid UserId,
    string VerificationCode
) : ICommand;
