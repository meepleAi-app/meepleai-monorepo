using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to unsuspend (reactivate) a user account.
/// </summary>
internal record UnsuspendUserCommand(
    string UserId
) : ICommand<UserDto>;
