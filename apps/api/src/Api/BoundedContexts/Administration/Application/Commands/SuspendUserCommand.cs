using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to suspend a user account.
/// Suspended users cannot login until unsuspended.
/// </summary>
/// <param name="UserId">The ID of the user to suspend.</param>
/// <param name="RequesterId">The ID of the admin requesting the suspension.</param>
/// <param name="Reason">Optional reason for suspension.</param>
internal record SuspendUserCommand(
    string UserId,
    Guid RequesterId,
    string? Reason = null
) : ICommand<UserDto>;
