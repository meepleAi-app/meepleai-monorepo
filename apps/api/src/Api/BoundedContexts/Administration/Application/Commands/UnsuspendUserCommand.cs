using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to unsuspend (reactivate) a user account.
/// </summary>
/// <param name="UserId">The ID of the user to unsuspend.</param>
/// <param name="RequesterId">The ID of the admin requesting the unsuspension.</param>
internal record UnsuspendUserCommand(
    string UserId,
    Guid RequesterId
) : ICommand<UserDto>;
