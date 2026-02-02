using MediatR;

namespace Api.BoundedContexts.Administration.Domain.Events;

/// <summary>
/// Domain event raised when dashboard statistics are updated.
/// Broadcast to all connected dashboard SSE clients.
/// </summary>
public record DashboardStatsUpdatedEvent : INotification
{
    /// <summary>
    /// Total games in collection.
    /// </summary>
    public int CollectionCount { get; init; }

    /// <summary>
    /// Total games played.
    /// </summary>
    public int PlayedCount { get; init; }

    /// <summary>
    /// Total active sessions.
    /// </summary>
    public int ActiveSessionCount { get; init; }

    /// <summary>
    /// Total users online.
    /// </summary>
    public int OnlineUserCount { get; init; }

    /// <summary>
    /// When the stats were updated.
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
