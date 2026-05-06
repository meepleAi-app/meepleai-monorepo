using Api.BoundedContexts.Administration.Application.Attributes;
using Api.BoundedContexts.Authentication.Application.Attributes;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to delete a user account.
/// Prevents self-deletion and deletion of the last admin.
/// </summary>
[AuditableAction("UserDelete", "User", Level = 2)]
[RequireTwoFactor(Reason = "Irreversible destruction of user data; must be 2FA-guarded.")]
internal record DeleteUserCommand(
    string UserId,
    string RequestingUserId
) : ICommand;
