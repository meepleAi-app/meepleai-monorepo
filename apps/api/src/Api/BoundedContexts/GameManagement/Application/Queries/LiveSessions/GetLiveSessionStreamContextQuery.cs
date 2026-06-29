using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Result of the live-session stream context query.
/// Combines session-found, caller-authorized, and companion-presence in one thin DTO
/// so the SSE endpoint (Task 4) can make all three decisions before writing SSE headers.
/// Issue #2561 SP2 T4.
/// </summary>
/// <param name="Found">True if a LiveGameSession with <c>SessionId</c> exists.</param>
/// <param name="Authorized">True if <c>UserId</c> is the session owner or an active player.</param>
/// <param name="HasCompanion">True if <c>TrackingSessionId</c> is non-null (companion was created by SP0 Saga).</param>
internal record LiveSessionStreamContextResult(bool Found, bool Authorized, bool HasCompanion);

/// <summary>
/// Query to resolve authz + companion-presence for the native SSE stream endpoint.
/// Returns a thin context result rather than throwing so the endpoint controls the HTTP response.
/// Issue #2561 SP2 T4.
/// </summary>
internal record GetLiveSessionStreamContextQuery(Guid SessionId, Guid UserId)
    : IQuery<LiveSessionStreamContextResult>;
