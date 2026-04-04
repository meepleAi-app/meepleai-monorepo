using Api.BoundedContexts.Administration.Application.Attributes;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to change a user's role.
/// ADM-002: AdminRole required to enforce role hierarchy privilege checks.
/// </summary>
/// <param name="UserId">The ID of the user whose role is being changed.</param>
/// <param name="NewRole">The new role to assign.</param>
/// <param name="Reason">Optional reason for the role change.</param>
/// <param name="AdminRole">The role of the admin performing this action (from session). Used for privilege level validation.</param>
[AuditableAction("UserRoleChange", "User", Level = 1)]
internal record ChangeUserRoleCommand(
    string UserId,
    string NewRole,
    string? Reason = null,
    string AdminRole = "admin"
) : ICommand<UserDto>;
