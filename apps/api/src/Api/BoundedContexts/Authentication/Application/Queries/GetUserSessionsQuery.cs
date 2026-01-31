using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get all active sessions for a specific user.
/// </summary>
internal record GetUserSessionsQuery(Guid UserId) : IQuery<List<SessionInfo>>;
