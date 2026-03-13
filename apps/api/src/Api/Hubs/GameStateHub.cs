// GameStateHub - SignalR Hub for real-time game state updates
// Issue #2406: Game State Editor UI - Backend Integration
//
// Provides real-time bidirectional communication for game state changes.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// SignalR hub for real-time game state synchronization across clients.
/// Supports both JWT-authenticated users and guest participants (via session token).
/// AllowAnonymous permits guest WebSocket connections; hub methods validate identity as needed.
/// </summary>
[AllowAnonymous]
public class GameStateHub : Hub
{
    private readonly ILogger<GameStateHub> _logger;

    public GameStateHub(ILogger<GameStateHub> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Join a game session group to receive state updates.
    /// </summary>
    public async Task JoinSession(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, GetSessionGroup(sessionId)).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} joined session {SessionId} (Connection: {ConnectionId})",
            Context.UserIdentifier,
            sessionId,
            Context.ConnectionId
        );
    }

    /// <summary>
    /// Leave a game session group.
    /// </summary>
    public async Task LeaveSession(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GetSessionGroup(sessionId)).ConfigureAwait(false);

        _logger.LogInformation(
            "User {UserId} left session {SessionId} (Connection: {ConnectionId})",
            Context.UserIdentifier,
            sessionId,
            Context.ConnectionId
        );
    }

    /// <summary>
    /// Broadcast state change to all clients in session except sender.
    /// </summary>
    public async Task BroadcastStateChange(string sessionId, object stateChange)
    {
        var message = new
        {
            type = "state-changed",
            sessionId,
            data = stateChange,
            userId = Context.UserIdentifier,
            timestamp = DateTime.UtcNow
        };

        await Clients
            .OthersInGroup(GetSessionGroup(sessionId))
            .SendAsync("StateChanged", message).ConfigureAwait(false);

        _logger.LogDebug(
            "State change broadcast for session {SessionId} by user {UserId}",
            sessionId,
            Context.UserIdentifier
        );
    }

    /// <summary>
    /// Notify clients of a conflict detected during concurrent editing.
    /// </summary>
    public async Task NotifyConflict(string sessionId, object conflict)
    {
        var message = new
        {
            type = "conflict-detected",
            sessionId,
            data = conflict,
            userId = Context.UserIdentifier,
            timestamp = DateTime.UtcNow
        };

        await Clients
            .Group(GetSessionGroup(sessionId))
            .SendAsync("StateChanged", message).ConfigureAwait(false);

        _logger.LogWarning(
            "Conflict detected in session {SessionId} by user {UserId}",
            sessionId,
            Context.UserIdentifier
        );
    }

    /// <summary>
    /// Notify clients that a snapshot was created.
    /// </summary>
    public async Task NotifySnapshotCreated(string sessionId, object snapshot)
    {
        var message = new
        {
            type = "snapshot-created",
            sessionId,
            data = snapshot,
            userId = Context.UserIdentifier,
            timestamp = DateTime.UtcNow
        };

        await Clients
            .Group(GetSessionGroup(sessionId))
            .SendAsync("StateChanged", message).ConfigureAwait(false);

        _logger.LogInformation(
            "Snapshot created in session {SessionId} by user {UserId}",
            sessionId,
            Context.UserIdentifier
        );
    }

    /// <summary>
    /// Join a game session with a specific role.
    /// Host users are added to both the session group and a host-only sub-group.
    /// </summary>
    public async Task JoinSessionWithRole(string sessionId, string role)
    {
        var group = GetSessionGroup(sessionId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group).ConfigureAwait(false);

        if (string.Equals(role, "host", StringComparison.OrdinalIgnoreCase))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"{group}:host").ConfigureAwait(false);
        }

        _logger.LogInformation(
            "User {UserId} joined session {SessionId} with role {Role} (Connection: {ConnectionId})",
            Context.UserIdentifier,
            sessionId,
            role,
            Context.ConnectionId
        );
    }

    /// <summary>
    /// Player proposes a score — only the host group receives the proposal.
    /// </summary>
    public async Task ProposeScore(string sessionId, object proposal)
    {
        await Clients.Group($"{GetSessionGroup(sessionId)}:host")
            .SendAsync("ScoreProposed", proposal).ConfigureAwait(false);

        _logger.LogDebug(
            "Score proposed in session {SessionId} by user {UserId}",
            sessionId,
            Context.UserIdentifier
        );
    }

    /// <summary>
    /// Host confirms a score — broadcast to all participants in the session.
    /// </summary>
    public async Task ConfirmScore(string sessionId, object confirmation)
    {
        await Clients.Group(GetSessionGroup(sessionId))
            .SendAsync("ScoreConfirmed", confirmation).ConfigureAwait(false);

        _logger.LogInformation(
            "Score confirmed in session {SessionId} by user {UserId}",
            sessionId,
            Context.UserIdentifier
        );
    }

    /// <summary>
    /// Host toggles AI agent access for a specific participant.
    /// Broadcasts to session group because SignalR identity is JWT userId or "guest:{token}",
    /// not the participant DB GUID. Clients filter by participantId.
    /// </summary>
    public async Task UpdateAgentAccess(string sessionId, string participantId, bool enabled)
    {
        await Clients.Group(GetSessionGroup(sessionId))
            .SendAsync("AgentAccessChanged", new
            {
                participantId,
                enabled
            }).ConfigureAwait(false);

        _logger.LogInformation(
            "Agent access {Status} for participant {ParticipantId} in session {SessionId} by user {UserId}",
            enabled ? "enabled" : "disabled",
            participantId,
            sessionId,
            Context.UserIdentifier
        );
    }

    /// <summary>
    /// Handle client disconnection.
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

    private static string GetSessionGroup(string sessionId) => $"session:{sessionId}";
}
