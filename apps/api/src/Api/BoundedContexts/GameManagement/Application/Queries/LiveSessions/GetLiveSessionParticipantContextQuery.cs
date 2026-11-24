using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Result of the live-session participant authorization query.
/// <c>Found</c> = a session with <c>SessionId</c> exists; <c>Authorized</c> = <c>UserId</c> is the
/// session creator or an active linked player. Non-throwing so the RequireLiveSessionParticipant
/// endpoint filter maps the flags to 404/403. Issue #2573.
/// </summary>
/// <param name="Found">True if a <c>LiveGameSession</c> with <c>SessionId</c> exists.</param>
/// <param name="Authorized">True if <c>UserId</c> is the session creator or an active linked player.</param>
/// <param name="IsCreator">
/// True if <c>UserId</c> is the session creator (<c>CreatedByUserId</c>), whom the RequireLiveSessionCreator
/// filter lets through (#4763). Defaults to false, so a result built without it refuses.
/// </param>
internal record LiveSessionParticipantContextResult(bool Found, bool Authorized, bool IsCreator = false);

/// <summary>
/// Query to resolve per-session participant authorization for live-session write/read endpoints.
/// Mirrors <c>GetLiveSessionStreamContextQuery</c> without companion-presence. Issue #2573.
/// </summary>
internal record GetLiveSessionParticipantContextQuery(Guid SessionId, Guid UserId)
    : IQuery<LiveSessionParticipantContextResult>;
