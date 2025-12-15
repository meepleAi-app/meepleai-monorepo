using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get a user by email address.
/// </summary>
internal record GetUserByEmailQuery(string Email) : IQuery<UserDto?>;
