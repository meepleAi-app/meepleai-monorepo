using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get a user by ID.
/// </summary>
public record GetUserByIdQuery(string UserId) : IQuery<UserDto?>;
