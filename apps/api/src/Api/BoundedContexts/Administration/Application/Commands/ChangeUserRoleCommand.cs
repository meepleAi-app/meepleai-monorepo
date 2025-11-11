using Api.Models;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to change a user's role.
/// </summary>
public record ChangeUserRoleCommand(
    string UserId,
    string NewRole
) : IRequest<UserDto>;
