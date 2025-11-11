using Api.Models;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get a user by email address.
/// </summary>
public record GetUserByEmailQuery(string Email) : IRequest<UserDto?>;
