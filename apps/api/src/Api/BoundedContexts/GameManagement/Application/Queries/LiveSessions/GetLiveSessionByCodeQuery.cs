using Api.BoundedContexts.GameManagement.Application.DTOs.LiveSessions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;

/// <summary>
/// Query to retrieve a live session by its join code.
/// Issue #4749: CQRS queries for live sessions.
/// </summary>
/// <param name="SessionCode">The join code.</param>
/// <param name="RequestedBy">
/// The authenticated caller, derived from the principal (#4776). #4939: the full <see cref="LiveSessionDto"/>
/// (creator, players' user ids, notes, score history) is for the creator or an active player
/// (<c>LiveGameSession.IsAuthorizedParticipant</c>); anyone else gets the 404 of an unknown code. A lobby
/// preview for whoever holds the code is <c>GET /live-sessions/code/{code}/public</c> (#2590).
/// </param>
internal record GetLiveSessionByCodeQuery(string SessionCode, Guid RequestedBy) : IQuery<LiveSessionDto>;
