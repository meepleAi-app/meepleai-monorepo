using Api.BoundedContexts.Administration.Application.Attributes;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to change a user's role.
/// </summary>
[AuditableAction("UserRoleChange", "User", Level = 1)]
internal record ChangeUserRoleCommand(
    string UserId,
    string NewRole
) : ICommand<UserDto>;
