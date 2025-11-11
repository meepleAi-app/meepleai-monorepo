using Api.Models;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get a user by ID.
/// </summary>
public record GetUserByIdQuery(string UserId) : IRequest<UserDto?>;
