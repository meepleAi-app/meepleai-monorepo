using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to change a user's role.
/// </summary>
public record ChangeUserRoleCommand(
    string UserId,
    string NewRole
) : ICommand<UserDto>;
