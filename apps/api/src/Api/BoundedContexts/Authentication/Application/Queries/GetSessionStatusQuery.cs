using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to get the status of a specific session.
/// User must own the session OR have Admin role.
/// </summary>
internal record GetSessionStatusQuery(
    Guid SessionId,
    Guid RequestingUserId,
    bool IsRequestingUserAdmin
) : IQuery<SessionInfo?>;
