using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to retrieve the currently authenticated user from a session ID.
/// Used by /auth/me endpoint to materialize the user DTO from the active session.
/// </summary>
internal record GetCurrentUserQuery(Guid SessionId) : IQuery<UserDto?>;
