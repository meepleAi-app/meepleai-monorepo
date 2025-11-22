using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to retrieve a user by their ID.
/// </summary>
public record GetUserByIdQuery(
    Guid UserId
) : IQuery<UserDto?>;
