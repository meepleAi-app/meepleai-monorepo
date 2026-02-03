using Api.BoundedContexts.Authentication.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to set a user's level (admin only).
/// Issue #3141: Allow admins to manually adjust user level.
/// </summary>
/// <param name="UserId">User ID to update</param>
/// <param name="Level">New level value (must be >= 0)</param>
internal sealed record SetUserLevelCommand(
    Guid UserId,
    int Level
) : IRequest<UserDto>;
