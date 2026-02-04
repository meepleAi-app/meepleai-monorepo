using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Commands.AccountLockout;

/// <summary>
/// Command to manually unlock a user account (admin only).
/// Issue #3339: Account lockout after failed login attempts.
/// </summary>
internal record UnlockAccountCommand(
    Guid UserId,
    Guid AdminId
) : ICommand<UnlockAccountResult>;

/// <summary>
/// Result of the unlock account operation.
/// </summary>
internal record UnlockAccountResult(
    Guid UserId,
    string Email,
    string Message
);
