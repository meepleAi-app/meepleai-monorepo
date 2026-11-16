using Api.BoundedContexts.Authentication.Application.Attributes;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to reset a user's password.
/// </summary>
/// <param name="UserId">The user whose password is reset.</param>
/// <param name="NewPassword">The new password.</param>
/// <param name="AdminRole">
/// The role of the admin acting (from the session). #4907: the target must have a role below it, or a reset hands
/// over an account of the same rank or higher. The default refuses every target.
/// </param>
/// <remarks>
/// Not <c>[AuditableAction]</c> on purpose: <c>AuditLoggingBehavior</c> copies every property of the command into
/// the audit metadata except a closed list of names, and <c>NewPassword</c> is not in it.
/// </remarks>
[RequireTwoFactor(Reason = "A password reset hands over the account; it must be 2FA-guarded.")]
internal record ResetUserPasswordCommand(
    string UserId,
    string NewPassword,
    string AdminRole = "user"
) : ICommand;
