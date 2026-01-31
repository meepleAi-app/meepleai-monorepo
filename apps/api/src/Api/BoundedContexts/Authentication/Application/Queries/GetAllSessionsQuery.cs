using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get all sessions (optionally filtered by user ID) for admin purposes.
/// </summary>
internal record GetAllSessionsQuery(
    Guid? UserId = null,
    int Limit = 100
) : IQuery<List<SessionInfo>>;
