using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to get the CALLER'S active game sessions (Setup, InProgress, Paused).
/// Issue #2755: Returns paginated response to match frontend schema.
///
/// <para>🔴 Issue #4080: <paramref name="OwnerUserId"/> is positional and has NO default
/// ON PURPOSE. Before this fix the query carried no caller identity at all, so the endpoint
/// returned every user's active sessions to anyone authenticated — including a freshly
/// registered account at first login, which saw other people's <c>playerName</c>, <c>notes</c>
/// and <c>scoreData</c>. Giving this a default (or making it nullable) restores that
/// behaviour for any caller that forgets it: the failure would be fail-OPEN and silent.
/// Keep it required so the compiler enumerates every construction site.</para>
///
/// <para>Ownership is <c>GameSession.CreatedByUserId</c>, which is the ONLY user link this
/// aggregate has — <c>GameSessionEntity</c> stores players in <c>PlayersJson</c>, and
/// <c>session_players.user_id</c> belongs to <c>LiveGameSession</c> instead. "A session I
/// joined but did not create" therefore does not exist here; it lives on the other aggregate
/// and is already served by <c>GET /api/v1/live-sessions/active</c>
/// (<c>GetUserActiveSessionsQuery</c>). Per ADR-089 the two are not to be reconciled.</para>
/// </summary>
internal record GetActiveSessionsQuery(
    Guid OwnerUserId,
    int? Limit = null,
    int? Offset = null
) : IQuery<PaginatedSessionsResponseDto>;
