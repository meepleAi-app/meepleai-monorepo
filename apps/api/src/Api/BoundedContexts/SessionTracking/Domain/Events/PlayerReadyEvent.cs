using MediatR;

namespace Api.BoundedContexts.SessionTracking.Domain.Events;

/// <summary>
/// Raised when a player marks themselves as ready for the next phase/turn.
/// Issue #4765 - Player Action Endpoints
/// </summary>
public record PlayerReadyEvent : INotification
{
    public required Guid SessionId { get; init; }
    public required Guid ParticipantId { get; init; }
    public required string DisplayName { get; init; }
    public required bool IsReady { get; init; }
    public required int ReadyCount { get; init; }
    public required int TotalPlayers { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
