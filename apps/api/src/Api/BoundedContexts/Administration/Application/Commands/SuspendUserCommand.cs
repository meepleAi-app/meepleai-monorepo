using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to suspend a user account.
/// Suspended users cannot login until unsuspended.
/// </summary>
internal record SuspendUserCommand(
    string UserId,
    string? Reason = null
) : ICommand<UserDto>;
