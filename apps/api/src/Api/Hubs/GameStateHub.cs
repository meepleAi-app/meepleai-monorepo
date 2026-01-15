// GameStateHub - SignalR Hub for real-time game state updates
// Issue #2406: Game State Editor UI - Backend Integration
//
// Provides real-time bidirectional communication for game state changes.

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Api.Hubs;

/// <summary>
/// SignalR hub for real-time game state synchronization across clients.
/// </summary>
[Authorize]
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
