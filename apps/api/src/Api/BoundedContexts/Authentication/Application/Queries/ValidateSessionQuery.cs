using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to validate a session token and retrieve user information.
/// </summary>
public record ValidateSessionQuery(
    string SessionToken
) : IQuery<SessionStatusDto>;
