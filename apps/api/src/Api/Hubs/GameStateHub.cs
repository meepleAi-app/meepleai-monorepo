// GameStateHub - SignalR Hub for real-time game state updates
// Issue #2406: Game State Editor UI - Backend Integration
// Game Night Improvvisata: Added Improvvisata-specific hub methods (Task 10).
// #4962: joining a session group requires being a participant of that live session; relaying into a
// group requires having joined it. The server notifies through IHubContext<GameStateHub>, not hub methods.

using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// SignalR hub for real-time updates of a live game session (group <c>session:{liveSessionId}</c>).
/// <para>
/// The connection itself is anonymous-friendly (<see cref="AllowAnonymousAttribute"/>), but nothing is
/// reachable without an identity: <see cref="JoinSession"/> admits only the creator or an active linked
/// player of the <c>LiveGameSession</c>, judged on the authenticated user of the connection (the session
/// cookie, authenticated for <c>/hubs</c> by <c>SessionAuthenticationHandler</c>) and never on an id passed
/// as an argument (ADR-096, #4776). A non-participant and an unknown session get the same error.
/// </para>
/// <para>
/// Everything the server tells a session (verdicts, pause, scores, agent access, score proposals) goes
/// through <c>IHubContext&lt;GameStateHub&gt;</c> from the event and command handlers. Until #4962 the hub
/// also exposed those sends as methods any connection could invoke on any session, plus a
/// <c>JoinSessionWithRole</c> that trusted a client-chosen "host" role: they had no client caller and are gone.
/// </para>
/// </summary>
[AllowAnonymous]
public class GameStateHub : Hub
{
    /// <summary>Same text for an unknown session and for one the caller does not take part in.</summary>
    internal const string SessionNotFoundMessage = "Live session not found.";

    internal const string AuthenticationRequiredMessage = "Sign in to follow a live session.";

    internal const string NotJoinedMessage = "Join the live session before sending to it.";

    private const string JoinedSessionsKey = "gamestate:joined-sessions";

    private readonly ILogger<GameStateHub> _logger;
    private readonly ISender _sender;

    public GameStateHub(ILogger<GameStateHub> logger, ISender sender)
    {
        _logger = logger;
        _sender = sender;
    }

    /// <summary>
    /// Join a live session group to receive its updates. Only the session creator or an active linked
    /// player is admitted (the rule of <c>RequireLiveSessionParticipant</c>, #2573).
    /// </summary>
    /// <exception cref="HubException">Unauthenticated connection, or a session the caller cannot see.</exception>
    public async Task JoinSession(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out var liveSessionId))
        {
            throw new HubException(SessionNotFoundMessage);
        }

        var userId = Context.User?.GetUserId() ?? Guid.Empty;
        if (userId == Guid.Empty)
        {
            throw new HubException(AuthenticationRequiredMessage);
        }

        if (!await IsParticipantAsync(liveSessionId, userId).ConfigureAwait(false))
        {
            throw new HubException(SessionNotFoundMessage);
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroup(liveSessionId)).ConfigureAwait(false);
        JoinedSessions().Add(liveSessionId);

        _logger.LogInformation(
            "User {UserId} joined session {SessionId} (Connection: {ConnectionId})",
            userId,
            liveSessionId,
            Context.ConnectionId);
    }

    /// <summary>
    /// Leave a live session group. Leaving a group the connection is not in does nothing.
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out var liveSessionId))
        {
            return;
        }

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetSessionGroup(liveSessionId)).ConfigureAwait(false);
        JoinedSessions().Remove(liveSessionId);

        _logger.LogInformation(
            "User {UserId} left session {SessionId} (Connection: {ConnectionId})",
            Context.UserIdentifier,
            liveSessionId,
            Context.ConnectionId);
    }

    /// <summary>
    /// Relay a game-state change to the other clients of a session the connection has joined
    /// (game-state editor, #2406). The payload is the editor's state and is relayed as is.
    /// </summary>
    /// <remarks>
    /// Participation is checked again on every relay, not only at join: a player removed from the live
    /// session is taken out of the group here and refused.
    /// </remarks>
    /// <exception cref="HubException">The connection has not joined that session, or no longer takes part.</exception>
    public async Task BroadcastStateChange(string sessionId, object stateChange)
    {
        var liveSessionId = RequireJoined(sessionId);

        var userId = Context.User?.GetUserId() ?? Guid.Empty;
        if (userId == Guid.Empty || !await IsParticipantAsync(liveSessionId, userId).ConfigureAwait(false))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetSessionGroup(liveSessionId)).ConfigureAwait(false);
            JoinedSessions().Remove(liveSessionId);
            throw new HubException(NotJoinedMessage);
        }

        var message = new
        {
            type = "state-changed",
            sessionId = liveSessionId.ToString(),
            data = stateChange,
            userId = Context.UserIdentifier,
            timestamp = DateTime.UtcNow
        };

        await Clients
            .OthersInGroup(GetSessionGroup(liveSessionId))
            .SendAsync("StateChanged", message).ConfigureAwait(false);

        _logger.LogDebug(
            "State change broadcast for session {SessionId} by user {UserId}",
            liveSessionId,
            Context.UserIdentifier);
    }

    /// <summary>
    /// Handle client disconnection. Group membership and the joined-session set end with the connection.
    /// </summary>
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (exception != null)
        {
            _logger.LogWarning(
                exception,
                "Client disconnected with error: {ConnectionId}, User: {UserId}",
                Context.ConnectionId,
                Context.UserIdentifier
            );
        }
        else
        {
            _logger.LogInformation(
                "Client disconnected: {ConnectionId}, User: {UserId}",
                Context.ConnectionId,
                Context.UserIdentifier
            );
        }

        await base.OnDisconnectedAsync(exception).ConfigureAwait(false);
    }

    /// <summary>
    /// The rule of <c>RequireLiveSessionParticipant</c> (#2573): creator or active linked player. Unknown
    /// session and non-participant are the same answer to the caller.
    /// </summary>
    private async Task<bool> IsParticipantAsync(Guid liveSessionId, Guid userId)
    {
        var access = await _sender
            .Send(new GetLiveSessionParticipantContextQuery(liveSessionId, userId), Context.ConnectionAborted)
            .ConfigureAwait(false);

        if (access.Found && access.Authorized)
        {
            return true;
        }

        _logger.LogWarning(
            "Hub access refused for user {UserId} on session {SessionId} (found: {Found}) (Connection: {ConnectionId})",
            userId,
            liveSessionId,
            access.Found,
            Context.ConnectionId);
        return false;
    }

    private Guid RequireJoined(string sessionId)
    {
        if (!Guid.TryParse(sessionId, out var liveSessionId) || !JoinedSessions().Contains(liveSessionId))
        {
            throw new HubException(NotJoinedMessage);
        }

        return liveSessionId;
    }

    /// <summary>
    /// Sessions this connection was admitted to. <c>Context.Items</c> lives as long as the connection, and
    /// SignalR runs one invocation per connection at a time by default, so a plain set is enough.
    /// </summary>
    private HashSet<Guid> JoinedSessions()
    {
        if (Context.Items.TryGetValue(JoinedSessionsKey, out var existing) && existing is HashSet<Guid> joined)
        {
            return joined;
        }

        var created = new HashSet<Guid>();
        Context.Items[JoinedSessionsKey] = created;
        return created;
    }

    /// <summary>
    /// Canonical group name: the same <c>Guid</c> formatting the server-side senders use
    /// (<c>$"session:{sessionId}"</c> on a <see cref="Guid"/>), whatever casing the client sent.
    /// </summary>
    internal static string GetSessionGroup(Guid sessionId) => $"session:{sessionId}";
}
