using Api.Models;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to update existing user details (email, display name, role).
/// </summary>
public record UpdateUserCommand(
    string UserId,
    string? Email = null,
    string? DisplayName = null,
    string? Role = null
) : IRequest<UserDto>;
