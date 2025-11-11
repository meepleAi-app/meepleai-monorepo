using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to create a new user with specified role.
/// Admin users can create users with any role, bypassing normal registration restrictions.
/// </summary>
public record CreateUserCommand(
    string Email,
    string Password,
    string DisplayName,
    string Role = "user"
) : ICommand<UserDto>;
