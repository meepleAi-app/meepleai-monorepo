using Api.BoundedContexts.Administration.Application.Attributes;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to change a user's role.
/// </summary>
/// <param name="UserId">The ID of the user whose role is being changed.</param>
/// <param name="NewRole">The new role to assign.</param>
/// <param name="Reason">Optional reason for the role change.</param>
[AuditableAction("UserRoleChange", "User", Level = 1)]
internal record ChangeUserRoleCommand(
    string UserId,
    string NewRole,
    string? Reason = null
) : ICommand<UserDto>;
